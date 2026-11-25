using log4net.Appender;
using log4net.Core;

namespace SilkEngine.Core.LogSystem;

/// <summary>Bridges a custom <see cref="ILogSink"/> into the log4net pipeline.</summary>
internal sealed class LogSinkAppender : AppenderSkeleton
{
    private readonly ILogSink _sink;

    internal LogSinkAppender(ILogSink sink)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    internal void FlushSink() => _sink.Flush();

    protected override void Append(LoggingEvent loggingEvent)
    {
        string message = loggingEvent.MessageObject?.ToString() ?? "null";
        if (loggingEvent.ExceptionObject is { } error)
            message += Environment.NewLine + error;

        var record = new LogRecord(
            Log.FromLevel(loggingEvent.Level ?? Level.Info),
            message,
            loggingEvent.Properties["Caller"] as string ?? string.Empty,
            loggingEvent.Properties["CallerFile"] as string ?? string.Empty,
            loggingEvent.TimeStamp
        );

        _sink.Write(in record);
    }

    protected override void OnClose()
    {
        _sink.Flush();
        base.OnClose();
    }
}
