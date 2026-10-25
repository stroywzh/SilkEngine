namespace SilkEngine.Core.LogSystem;

/// <summary>Single formatting rule shared by the console and file sinks.</summary>
internal static class LogFormat
{
    /// <summary>Formats one record as the engine's log line.</summary>
    /// <param name="record">Record to format.</param>
    /// <returns>The formatted line, including the caller file on its own line.</returns>
    public static string Format(in LogRecord record) =>
        $"[{record.Timestamp:HH:mm:ss.fff}]{record.CallerMember}: {record.Message}\n{record.CallerFile}";
}
