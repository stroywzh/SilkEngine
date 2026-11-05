namespace SilkEngine.Core.LogSystem;

/// <summary>
/// Fan-out sink: hands every record to all installed sinks, used to keep the console and the log file
/// in sync.
/// </summary>
/// <remarks>
/// The sink list is fixed at construction and never mutated, so <see cref="Write"/> is safe to call
/// from any thread. A destination that throws is isolated: the remaining sinks still receive the
/// record, because logging must never fail business code.
/// </remarks>
internal sealed class CompositeLogSink : ILogSink
{
    private readonly ILogSink[] _sinks;

    /// <summary>Creates a sink that forwards to <paramref name="sinks"/> in order.</summary>
    public CompositeLogSink(params ILogSink[] sinks)
    {
        ArgumentNullException.ThrowIfNull(sinks);
        if (sinks.Length == 0)
            throw new ArgumentException("A composite sink needs at least one inner sink.", nameof(sinks));

        _sinks = (ILogSink[])sinks.Clone();
    }

    /// <inheritdoc />
    public void Write(in LogRecord record)
    {
        foreach (ILogSink sink in _sinks)
        {
            try
            {
                sink.Write(in record);
            }
            catch (Exception)
            {
                // Best effort: one broken destination must not drop the record for the others.
            }
        }
    }

    /// <inheritdoc />
    public void Flush()
    {
        foreach (ILogSink sink in _sinks)
        {
            try
            {
                sink.Flush();
            }
            catch (Exception)
            {
                // Best effort: an unavailable destination must not stop the remaining flushes.
            }
        }
    }
}
