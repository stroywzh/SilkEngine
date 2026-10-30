using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using SilkEngine.Core.LogSystem;

namespace SilkEngine;

/// <summary>Severity of a log record; larger values are more severe.</summary>
public enum LogLevel : byte
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
}

/// <summary>
/// The single logging facade. Every record goes through the installed <see cref="ILogSink"/>; the
/// default sink writes to the console.
/// </summary>
/// <remarks>
/// Gating has two independent switches: <see cref="MinLevel"/> (severity) and
/// <see cref="Application.LogSetting"/>. There deliberately is no <c>Log.Enable(category)</c> API now.
/// </remarks>
public static class Log
{
    private static ILogSink _sink = new ConsoleLogSink();
    private static FileLogSink? _fileSink;

    /// <summary>Minimum level that is written; records below it are dropped.</summary>
    public static LogLevel MinLevel { get; set; } = LogLevel.Debug;

    /// <summary>Replaces the log sink.</summary>
    /// <param name="sink">New sink, or <c>null</c> to restore the default console sink.</param>
    /// <remarks>
    /// The installed sink is replaced as a whole, including the console+file pair that
    /// <see cref="EnableFileLog"/> installs; the file sink itself keeps running until
    /// <see cref="DisableFileLog"/> or the next <see cref="EnableFileLog"/>. The previous sink is
    /// flushed (best effort through its own implementation) before the swap.
    /// </remarks>
    public static void SetSink(ILogSink? sink)
    {
        var next = sink ?? new ConsoleLogSink();
        var previous = Interlocked.Exchange(ref _sink, next);
        previous.Flush();
    }

    /// <summary>
    /// Writes records to the console and to a file; returns the file receiving them.
    /// <br/> Will create default Log directory if no other dir is in.
    /// </summary>
    /// <returns>Absolute path of the log file that receives new records.</returns>
    public static string EnableFileLog(string? directory = null)
    {
        // Pending lines belong to the current file: write them before the sink is replaced.
        _fileSink?.DrainOnOwnerThread(int.MaxValue);

        var path = string.IsNullOrEmpty(directory) ? LogPaths.GetLogPath() : directory!;
        var sink = new FileLogSink(path);
        var previous = Interlocked.Exchange(ref _fileSink, sink);

        // Fan out instead of replacing: the console keeps the records visible while the file keeps
        // them durable.
        SetSink(new CompositeLogSink(new ConsoleLogSink(), sink));
        previous?.Dispose();
        return sink.LogFile;
    }

    /// <summary>Stops file logging: flushes and disposes the file sink and restores the console sink.</summary>
    public static void DisableFileLog()
    {
        var previous = Interlocked.Exchange(ref _fileSink, null);
        if (previous is null)
            return;

        SetSink(null);
        previous.Dispose();
    }

    /// <summary>Flushes the installed sink (waits for buffered records to reach their destination).</summary>
    public static void Flush() => Volatile.Read(ref _sink).Flush();

    /// <summary>Makes the calling thread the owner of file IO; called by the asset worker when it starts.</summary>
    internal static void AttachIoOwner() => LogIoQueue.AttachOwner();

    /// <summary>Releases file IO ownership so later records fall back to direct writes.</summary>
    internal static void DetachIoOwner() => LogIoQueue.DetachOwner();

    /// <summary>Writes queued file records on the IO owner thread, bounded so a burst cannot delay its work.</summary>
    internal static void PumpFileIo(int maxLines = LogIoQueue.DrainBudget) =>
        _fileSink?.DrainOnOwnerThread(maxLines);

    /*
    * The APIs below deliberately carry no XML documentation: their behaviour is the agreed contract.
    *
    */
    public static void Debug(
        object message,
        [CallerMemberName] string callerName = "",
        [CallerFilePath] string callerFile = ""
    )
    {
        if (Application.LogSetting.EnableLog)
        {
            Write(LogLevel.Debug, message, callerName, callerFile);
        }
    }

    public static void Info(
        object message,
        [CallerMemberName] string callerName = "",
        [CallerFilePath] string callerFile = ""
    ) => Write(LogLevel.Info, message, callerName, callerFile);

    public static void Warning(
        object message,
        [CallerMemberName] string callerName = "",
        [CallerFilePath] string callerFile = ""
    ) => Write(LogLevel.Warning, message, callerName, callerFile);

    public static void Error(
        object message,
        [CallerMemberName] string callerName = "",
        [CallerFilePath] string callerFile = ""
    ) => Write(LogLevel.Error, message, callerName, callerFile);

    public static void Exception(
        Exception exception,
        [CallerMemberName] string callerName = "",
        [CallerFilePath] string callerFile = ""
    ) => WriteWithException(LogLevel.Error, exception, callerName, callerFile);

    /// <summary>Formats an exception plus the calling stack and writes it at <paramref name="level"/>.</summary>
    private static void WriteWithException(
        LogLevel level,
        Exception exception,
        string callerName = "",
        string callerFile = ""
    )
    {
        var sb = new StringBuilder(exception?.ToString() ?? "null").AppendLine();
        var frames = new StackTrace(1, true).GetFrames();
        if (frames != null)
        {
            foreach (var f in frames)
            {
                sb.AppendLine($"  at {f}");
            }
        }

        Write(level, sb.ToString().TrimEnd(), callerName, callerFile);
    }

    private static void Write(LogLevel level, object message, string callerName, string callerFile)
    {
        if (level < MinLevel)
            return;

        var record = new LogRecord(
            level,
            message?.ToString() ?? "null",
            callerName,
            callerFile,
            DateTime.Now
        );

        Volatile.Read(ref _sink).Write(in record);
    }
}
