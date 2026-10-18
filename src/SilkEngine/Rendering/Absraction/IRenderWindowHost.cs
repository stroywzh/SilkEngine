namespace SilkEngine.Rendering.Abstraction;

/// <summary>Optional window event and lifetime capability, always driven on the render thread.</summary>
/// <remarks>Output/presenting stays a backend responsibility; this interface reports closing state,
/// the drawable size, and pumps native events. There is deliberately no callback-based render entry.</remarks>
public interface IRenderWindowHost
{
    /// <summary>Gets whether new rendering must stop because the window is closing.</summary>
    bool IsClosing { get; }

    /// <summary>Gets the drawable size in pixels; a zero dimension means "not known yet".</summary>
    /// <remarks>Read on the render thread after <see cref="PumpEvents"/>, so a pending resize is
    /// already reflected when the value is observed.</remarks>
    (int Width, int Height) FramebufferSize { get; }

    /// <summary>Pumps window events without executing a submit.</summary>
    /// <remarks>Exceptions propagate to the render loop and fault its cross-thread protocol.</remarks>
    void PumpEvents();
}
