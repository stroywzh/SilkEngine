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
    /// <remarks>The previous sink is flushed (best effort through its own implementation) before the swap.</remarks>
    public static void SetSink(ILogSink? sink)
    {
        var next = sink ?? new ConsoleLogSink();
        var previous = Interlocked.Exchange(ref _sink, next);
        previous.Flush();
    }

    /// <summary>
    /// Installs a file sink so records are also written to disk; returns the file receiving them.
    /// <br/> Will create default Log directory if no other dir is in.
    /// </summary>
    /// <returns>Absolute path of the log file that receives new records.</returns>
    /// <exception cref="ArgumentException"><paramref name="directory"/> is empty or whitespace.</exception>
    public static string EnableFileLog(string? directory = null)
    {
        var path = string.IsNullOrEmpty(directory) ? LogPaths.GetLogPath() : directory!;
        var sink = new FileLogSink(path);
        var previous = Interlocked.Exchange(ref _fileSink, sink);

        SetSink(sink);
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

    /*
    * 下面的 API 不需要加入任何注释，内容也是约定的内容
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
