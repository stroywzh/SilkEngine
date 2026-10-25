using System;

namespace SilkEngine.Core.LogSystem;

/// <summary>One formatted log record handed to a sink.</summary>
/// <param name="Level">Severity of the record.</param>
/// <param name="Message">Message text (already stringified by the facade).</param>
/// <param name="CallerMember">Caller member captured by the compiler.</param>
/// <param name="CallerFile">Caller file captured by the compiler.</param>
/// <param name="Timestamp">Local time at which the facade accepted the record.</param>
public readonly record struct LogRecord(
    LogLevel Level,
    string Message,
    string CallerMember,
    string CallerFile,
    DateTime Timestamp
);

/// <summary>
/// Destination of log records. The engine writes only through <see cref="Log"/>; this interface is
/// the single injection point (<see cref="Log.SetSink"/>).
/// </summary>
public interface ILogSink
{
    /// <summary>Accepts one record.</summary>
    /// <param name="record">Record to store or print.</param>
    /// <remarks>Called from any thread; implementations must be thread-safe.</remarks>
    void Write(in LogRecord record);

    /// <summary>Flushes buffered records to their destination.</summary>
    void Flush();
}
