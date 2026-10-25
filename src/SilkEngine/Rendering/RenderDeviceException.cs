using System;

namespace SilkEngine.Rendering;

/// <summary>
/// Thrown when the rendering device or the recorded command stream violates its contract, for
/// example when a recorded command id has no executor in the current assembly.
/// </summary>
/// <remarks>
/// Lives in <c>SilkEngine.Rendering</c> (engine assembly, C-10): the rendering base classes and the
/// generated command table throw it, and implementation assemblies must be able to catch it.
/// </remarks>
public sealed class RenderDeviceException : EngineException
{
    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">Description of the contract violation.</param>
    public RenderDeviceException(string message)
        : base(message) { }

    /// <summary>Creates the exception with a message and the underlying failure.</summary>
    /// <param name="message">Description of the contract violation.</param>
    /// <param name="innerException">The failure that caused it.</param>
    public RenderDeviceException(string message, Exception innerException)
        : base(message, innerException) { }
}
