using System.Diagnostics;
using System.Globalization;

namespace SilkEngine.Core.LogSystem;

/// <summary>
/// File-backed log sink: formatted records are handed to <see cref="LogIoQueue"/> and written by the thread
/// that owns file IO (the asset worker), so logging from a game thread never touches the disk.
/// </summary>
/// <remarks>
/// The sink owns no thread. It writes directly while no owner is attached (startup before the worker starts,
/// shutdown after it stops) or when the caller already is the owner, so the worker never waits on itself.
/// The writer is opened lazily and switches to a new file when the date rolls over.
/// </remarks>
internal sealed class FileLogSink : ILogSink, IDisposable
{
    private const string FilePrefix = "Log";
    private const int FlushTimeoutSeconds = 5;

    private readonly string _directory;
    private readonly object _writerGate = new();
    private StreamWriter? _writer;
    private string? _writerFile;
    private bool _disposed;

    /// <summary>Creates a sink that appends into <paramref name="logPath"/>; the directory is created when missing.</summary>
    /// <exception cref="ArgumentException"><paramref name="logPath"/> is null or empty.</exception>
    public FileLogSink(string logPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(logPath);
        Directory.CreateDirectory(logPath);
        _directory = logPath;
    }

    /// <summary>Path of the log file currently receiving records.</summary>
    public string LogFile
    {
        get
        {
            lock (_writerGate)
            {
                return _writerFile ?? FileOf(DateTime.Now);
            }
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// On the owner thread, or while no owner is attached, the line is written immediately. Otherwise it is
    /// queued and the owner writes it, so the caller never performs file IO and never blocks.
    /// </remarks>
    public void Write(in LogRecord record)
    {
        if (_disposed)
            return;

        string line = LogFormat.Format(record);
        if (!LogIoQueue.HasOwner || LogIoQueue.IsOwnerThread)
        {
            WriteLineToFile(line);
            return;
        }

        LogIoQueue.Enqueue(line);
    }

    /// <inheritdoc />
    /// <remarks>
    /// On the owner thread, or while no owner is attached, this drains the queue and flushes the writer.
    /// Otherwise it waits, bounded by <see cref="FlushTimeoutSeconds"/>, until the owner has written the
    /// records queued so far; every line is flushed on write, so a drained queue means written data.
    /// </remarks>
    public void Flush()
    {
        if (_disposed)
            return;

        if (!LogIoQueue.HasOwner || LogIoQueue.IsOwnerThread)
        {
            DrainQueue(int.MaxValue);
            FlushWriter();
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var timeout = TimeSpan.FromSeconds(FlushTimeoutSeconds);
        while (LogIoQueue.Count > 0 && stopwatch.Elapsed < timeout)
            Thread.Sleep(1);
    }

    /// <summary>Writes up to <paramref name="maxLines"/> queued records; called by the IO owner each loop.</summary>
    internal void DrainOnOwnerThread(int maxLines) => DrainQueue(maxLines);

    /// <summary>
    /// Writes everything still queued, flushes and closes the file; safe to call twice. Records that arrive
    /// afterwards are dropped.
    /// </summary>
    public void Dispose()
    {
        lock (_writerGate)
        {
            if (_disposed)
                return;
        }

        DrainQueue(int.MaxValue);

        lock (_writerGate)
        {
            _disposed = true;
            _writer?.Dispose();
            _writer = null;
            _writerFile = null;
        }
    }

    private void DrainQueue(int maxLines)
    {
        int written = 0;
        while (written < maxLines && LogIoQueue.TryDequeue(out string line))
        {
            WriteLineToFile(line);
            written++;
        }
    }

    private void WriteLineToFile(string line)
    {
        lock (_writerGate)
        {
            if (_disposed)
                return;

            StreamWriter writer = EnsureWriter();
            try
            {
                writer.WriteLine(line);
            }
            catch (IOException)
            {
                // Logging is best effort: an unavailable destination drops the line, the sink keeps working.
            }
        }
    }

    private StreamWriter EnsureWriter()
    {
        string file = FileOf(DateTime.Now);
        if (_writer is not null && string.Equals(file, _writerFile, StringComparison.Ordinal))
            return _writer;

        _writer?.Dispose();
        _writer = new StreamWriter(file, append: true) { AutoFlush = true };
        _writerFile = file;
        return _writer;
    }

    private void FlushWriter()
    {
        lock (_writerGate)
        {
            try
            {
                _writer?.Flush();
            }
            catch (IOException)
            {
                // Best effort, as above.
            }
        }
    }

    private string FileOf(DateTime value) =>
        Path.Combine(
            _directory,
            FilePrefix + value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".log"
        );
}
