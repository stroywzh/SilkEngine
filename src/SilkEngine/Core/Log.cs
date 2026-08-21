using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace SilkEngine.Core;

public static class Log
{
    private static readonly ConcurrentQueue<string> _queue = new();
    private static readonly AutoResetEvent _signal = new(false);
    private static readonly object _drainLock = new();
    private static readonly object _writersLock = new();
    private static volatile ILogWriter[] _writers = new ILogWriter[] { new ConsoleLogWriter() };
    private static volatile bool _running = true;
    private static readonly Thread _drainThread;

    static Log()
    {
        _drainThread = SilkEngine.Threading.ThreadFactory.CreateThread(DrainLoop, "LogDrain");
        _drainThread.Start();
    }

    internal static LogLevel MinLevel { get; set; } = LogLevel.Debug;

    internal static bool ShowThreadInfo { get; set; } = false;

    public static void AddWriter(ILogWriter writer)
    {
        lock (_drainLock)
        {
            lock (_writersLock)
            {
                var updated = new ILogWriter[_writers.Length + 1];
                Array.Copy(_writers, updated, _writers.Length);
                updated[^1] = writer;
                _writers = updated;
            }
        }
    }

    internal static void RemoveWriter(ILogWriter writer)
    {
        lock (_drainLock)
        {
            lock (_writersLock)
            {
                int index = Array.IndexOf(_writers, writer);
                if (index < 0)
                    return;

                var updated = new ILogWriter[_writers.Length - 1];
                Array.Copy(_writers, 0, updated, 0, index);
                Array.Copy(_writers, index + 1, updated, index, _writers.Length - index - 1);
                _writers = updated;
            }
        }
    }

    public static void Debug(object message) => Write(LogLevel.Debug, message);

    public static void Info(object message) => Write(LogLevel.Info, message);

    public static void Warn(object message) => Write(LogLevel.Warn, message);

    public static void Error(object message) => Write(LogLevel.Error, message);

    /// <summary>同步排空待写队列（由调用线程执行），返回时此前的日志消息已写入全部 writer。</summary>
    public static void Flush() => DrainOnce();

    internal static void StackTree(object message)
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

        _queue.Enqueue(line);
        _signal.Set();
    }

    private static void DrainLoop()
    {
        while (_running)
        {
            _signal.WaitOne();
            DrainOnce();
        }

        DrainOnce();
    }

    private static void DrainOnce()
    {
        lock (_drainLock)
        {
            ILogWriter[] writers = _writers;
            while (_queue.TryDequeue(out string? line))
            {
                foreach (var w in writers)
                {
                    w.Write(line);
                }
            }
        }
    }
}
