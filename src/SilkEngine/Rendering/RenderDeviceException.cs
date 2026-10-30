using System;

namespace SilkEngine.Rendering;

/// <summary>Thrown when the rendering device or command stream violates its contract.</summary>
public sealed class RenderDeviceException : EngineException
{
    /// <summary>Creates the exception with a message.</summary>
    public RenderDeviceException(string message)
        : base(message) { }

    /// <summary>Creates the exception with a message and the underlying failure.</summary>
    public RenderDeviceException(string message, Exception innerException)
        : base(message, innerException) { }
}
