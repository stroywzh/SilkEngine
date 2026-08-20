using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace SilkEngine.Core;

public static class Log
{
    private static readonly object _lock = new();
    private static readonly List<ILogWriter> _writers = new() { new ConsoleLogWriter() };

    public static LogLevel MinLevel { get; set; } = LogLevel.Debug;

    public static bool ShowThreadInfo { get; set; } = false;

    public static void AddWriter(ILogWriter writer)
    {
        lock (_lock)

            _writers.Add(writer);
    }

    public static void RemoveWriter(ILogWriter writer)
    {
        lock (_lock)

            _writers.Remove(writer);
    }

    public static void Debug(object message) => Write(LogLevel.Debug, message);

    public static void Info(object message) => Write(LogLevel.Info, message);

    public static void Warn(object message) => Write(LogLevel.Warn, message);

    public static void Error(object message) => Write(LogLevel.Error, message);

    public static void StackTree(object message)
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

        Write(LogLevel.Info, sb.ToString().TrimEnd());
    }

    private static void Write(LogLevel level, object message)
    {
        if (level < MinLevel)
            return;

        string text = message?.ToString() ?? "null";
        var now = DateTime.Now;
        string thread = ShowThreadInfo ? $"[{Thread.CurrentThread.Name ?? "Main"}]" : string.Empty;
        string line = $"[{now:HH:mm:ss.fff}]{thread}: {text}";

        lock (_lock)
        {
            foreach (var w in _writers)
            {
                w.Write(line);
            }
        }
    }
}
