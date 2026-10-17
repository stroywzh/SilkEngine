namespace SilkEngine.Rendering.Abstraction;

/// <summary>Optional window event and output capability, always driven on the render thread.</summary>
public interface IRenderWindowHost
{
    /// <summary>Gets whether new rendering must stop because the window is closing.</summary>
    bool IsClosing { get; }

    /// <summary>Pumps window events without executing a submitted render frame.</summary>
    /// <remarks>Exceptions propagate to the render loop and fault its cross-thread protocol.</remarks>
    void PumpEvents();

    /// <summary>Attempts one output iteration and synchronously invokes the callback at most once.</summary>
    /// <returns>True if the callback and output succeeded; false only when the callback did not run.</returns>
    /// <exception cref="ArgumentNullException">The callback is null.</exception>
    /// <remarks>Do not retain the callback after return. Callback/output failures propagate.
    /// True denotes CPU output completion, never GPU execution completion.</remarks>
    bool TryRenderFrame(Action executeCommands);
}
