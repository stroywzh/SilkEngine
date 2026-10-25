using System.Collections.Concurrent;
using System.Globalization;

namespace SilkEngine.Core.LogSystem;

/// <summary>
/// File-backed log sink: formatted records are queued and appended to a per-day file by one
/// background thread.
/// </summary>
/// <remarks>
/// Platform independent: the directory is supplied by the caller, so no OS branch is needed. The sink
/// is not installed by default; hosts opt in through <see cref="SilkEngine.Log.EnableFileLog"/>. The
/// writer is the only owner of the file handle and switches to a new file when the date rolls over. 
/// Disposing the sink stops the writer after draining the queue; a concurrent writer that loses 
/// the race is ignored instead of throwing into business code. 
/// </remarks>
internal sealed class FileLogSink : ILogSink, IDisposable
{
    private const string FilePrefix = "Log";
    private readonly string _directory;
    private readonly BlockingCollection<string> _queue = new();
    private readonly Thread _worker;
    private volatile bool _disposed;
    private volatile string _logFile;

    /// <summary>Creates a sink that appends into <paramref name="logPath"/>.Will auto create directory if is missing</summary>
    /// <exception cref="ArgumentException"><paramref name="logPath"/> is null or empty.</exception>
    public FileLogSink(string logPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(logPath);
        Directory.CreateDirectory(logPath);
        _directory = logPath;
        _logFile = FileOf(DateTime.Now);

        _worker = new Thread(Drain) { IsBackground = true, Name = "LogFileSink" };
        _worker.Start();
    }

    /// <summary>Path of the log file currently receiving records.</summary>
    public string LogFile => _logFile;

    /// <inheritdoc />
    /// <remarks>
    /// A record written while the sink is being disposed is dropped: the queue rejects it and the sink
    /// swallows the resulting exception, so logging never throws into business code.
    /// </remarks>
    public void Write(in LogRecord record)
    {
        if (_disposed)
            return;

        try
        {
            _queue.Add(LogFormat.Format(record));
        }
        catch (ObjectDisposedException)
        {
            // Lost the race with Dispose: the record is dropped on purpose.
        }
        catch (InvalidOperationException)
        {
            // CompleteAdding already ran (dispose in progress).
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Waits until the background writer has consumed everything queued so far, bounded to five
    /// seconds so an unavailable destination can never hang the caller.
    /// </remarks>
    public void Flush()
    {
        var spin = new SpinWait();
        var deadline = Environment.TickCount64 + 5000;
        while (true)
        {
            bool pending;
            try
            {
                pending = _queue.Count > 0;
            }
            catch (ObjectDisposedException)
            {
                return; // the sink was disposed while flushing
            }

            if (_disposed || !pending || Environment.TickCount64 >= deadline)
                return;

            spin.SpinOnce();
        }
    }

    /// <summary>Stops the background writer after draining the queue; safe to call twice.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _queue.CompleteAdding();
        _worker.Join();
        _queue.Dispose();
    }

    /// <summary>Consumes the queue and appends each record to the current day's file.</summary>
    /// <remarks>
    /// The date is re-evaluated per record, so a sink that outlives midnight switches file without the
    /// caller noticing. The writer owns the only handle: write errors drop the line but keep
    /// the loop draining, so Flush/Dispose stay responsive.
    /// </remarks>
    private void Drain()
    {
        StreamWriter? writer = null;
        string? currentFile = null;

        try
        {
            foreach (var line in _queue.GetConsumingEnumerable())
            {
                var file = FileOf(DateTime.Now);
                if (writer is null || !string.Equals(file, currentFile, StringComparison.Ordinal))
                {
                    writer?.Dispose();
                    writer = new StreamWriter(file, append: true) { AutoFlush = true };
                    currentFile = file;
                    _logFile = file;
                }

                try
                {
                    writer.WriteLine(line);
                }
                catch (IOException)
                {
                    // Logging is best effort: an unavailable destination drops the line, but the queue
                    // keeps draining so Flush() and Dispose() stay responsive.
                }
            }
        }
        finally
        {
            writer?.Dispose();
        }
    }

    private string FileOf(DateTime value) =>
        Path.Combine(
            _directory,
            FilePrefix + value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".log"
        );
}
