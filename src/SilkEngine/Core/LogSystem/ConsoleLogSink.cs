namespace SilkEngine.Core.LogSystem;

/// <summary>Default sink: writes every record to the console.</summary>
internal sealed class ConsoleLogSink : ILogSink
{
    /// <inheritdoc />
    public void Write(in LogRecord record) => Console.WriteLine(LogFormat.Format(record));

    /// <inheritdoc />
    public void Flush() => Console.Out.Flush();
}
