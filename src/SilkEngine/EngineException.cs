using System;

namespace SilkEngine;

/// <summary>Base class of every exception the engine itself throws.</summary>
/// <remarks>
/// The base lives in the root namespace; each subsystem adds its own sealed family next to the
/// type that throws it. Exceptions that must keep a BCL semantic (for example
/// <c>RenderThreadStoppedException</c>, which is an <see cref="OperationCanceledException"/>) do not
/// derive from this type.
/// </remarks>
public abstract class EngineException : Exception
{
    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">Description of the failure.</param>
    protected EngineException(string message)
        : base(message) { }

    /// <summary>Creates the exception with a message and the underlying failure.</summary>
    /// <param name="message">Description of the failure.</param>
    /// <param name="innerException">The failure that caused it.</param>
    protected EngineException(string message, Exception innerException)
        : base(message, innerException) { }
}
