using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using SilkEngine.Core.Log;

namespace SilkEngine;

public static class Log
{
    private static LogDriver _logger = new();
    internal static LogLevel MinLevel { get; set; } = LogLevel.Debug;

    public static void Debug(
        object message,
        [CallerMemberName] string callerName = "",
        [CallerFilePath] string callerFile = ""
    ) => Write(LogLevel.Debug, message, callerName, callerFile);

    public static void Info(
        object message,
        [CallerMemberName] string callerName = "",
        [CallerFilePath] string callerFile = ""
    ) => Write(LogLevel.Info, message, callerName, callerFile);

    public static void Warning(
        object message,
        [CallerMemberName] string callerName = "",
        [CallerFilePath] string callerFile = ""
    ) => Write(LogLevel.Warn, message, callerName, callerFile);

    public static void Error(
        object message,
        [CallerMemberName] string callerName = "",
        [CallerFilePath] string callerFile = ""
    ) => Write(LogLevel.Error, message, callerName, callerFile);

    /* this ver is not completed yet.
    public static void Exception(
        Exception ex,
        [CallerMemberName] string callerName = "",
        [CallerFilePath] string callerFile = ""
    ) => StackTree(LogLevel.Exception, ex, callerName, callerFile);
    */

    public static void Exception(
        object message,
        [CallerMemberName] string callerName = "",
        [CallerFilePath] string callerFile = ""
    ) => StackTree(LogLevel.Exception, message, callerName, callerFile);

    internal static void StackTree(
        LogLevel level,
        object message,
        string callerName = "",
        string callerFile = ""
    )
    {
        var st = new StackTrace(1, true);
        var frames = st.GetFrames();
        var sb = new StringBuilder(message?.ToString() ?? "null").AppendLine();
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

        string text = message?.ToString() ?? "null";
        var now = DateTime.Now;
        string line = $"[{now:HH:mm:ss.fff}]{callerName}: {text}\n{callerFile}";

        _logger.Record(line);
    }
}
