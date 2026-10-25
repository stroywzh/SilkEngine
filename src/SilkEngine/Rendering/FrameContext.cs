using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering;

/// <summary>Read-only snapshot of one frame, handed to the pipeline for recording.</summary>
/// <remarks>
/// The snapshot is produced once per frame by the render thread : <see cref="FrameIndex"/> is a
/// copy of <see cref="SilkEngine.Time.FrameIndex"/> at publish time.
/// </remarks>
public readonly struct FrameContext
{
    /// <summary>Pipeline that records this frame; <c>null</c> in <c>default</c>.</summary>
    public IRenderPipeline? Pipeline { get; }

    /// <summary>1-based index of the frame being recorded; zero for <c>default</c>.</summary>
    public ulong FrameIndex { get; }

    /// <summary>Creates a frame snapshot; the render thread is the only caller.</summary>
    /// <param name="pipeline">Pipeline that records the frame.</param>
    /// <param name="frameIndex">Frame index published for this frame.</param>
    internal FrameContext(IRenderPipeline? pipeline, ulong frameIndex)
    {
        Pipeline = pipeline;
        FrameIndex = frameIndex;
    }
}

/// <summary>Execution context handed to every command executor.</summary>
/// <remarks>
/// The device is readable by implementation assemblies but only the engine can bind it
/// (<c>internal set</c>): an executor observes the context, it never re-binds it.
/// <para>
/// The frame snapshot is <b>not</b> carried here : it belongs to the backend, which validates
/// it and uses it for frame-accurate diagnostics; Executors act on their device only.
/// </para>
/// </remarks>
public ref struct CommandContext
{
    /// <summary>Device that executes the recorded commands.</summary>
    public IRenderDevice Device { get; internal set; }

    /// <summary>Creates the execution context.</summary>
    /// <param name="device">Device to execute against.</param>
    internal CommandContext(IRenderDevice device)
    {
        Device = device;
    }
}
