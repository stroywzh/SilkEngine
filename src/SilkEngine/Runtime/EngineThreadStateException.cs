namespace SilkEngine.Runtime;

/// <summary>Thrown when an <see cref="EngineThread"/> lifecycle transition is invalid.</summary>
/// <remarks>
/// Covers both "started twice" and "cannot enter the running state"; the thread state machine never
/// silently re-enters a loop.
/// </remarks>
public sealed class EngineThreadStateException : EngineException
{
    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">Description of the invalid transition.</param>
    public EngineThreadStateException(string message)
        : base(message) { }

    /// <summary>Creates the exception with a message and the underlying failure.</summary>
    /// <param name="message">Description of the invalid transition.</param>
    /// <param name="innerException">The failure that caused it.</param>
    public EngineThreadStateException(string message, System.Exception innerException)
        : base(message, innerException) { }
}
