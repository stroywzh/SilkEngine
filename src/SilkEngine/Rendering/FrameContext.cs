using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering;

/// <summary>Read-only snapshot of one frame, handed to the pipeline for recording.</summary>
public readonly struct FrameContext
{
    /// <summary>Pipeline that records this frame; <c>null</c> in <c>default</c>.</summary>
    public IRenderPipeline? Pipeline { get; }

    /// <summary>1-based index of the frame being recorded; zero for <c>default</c>.</summary>
    public ulong FrameIndex { get; }

    /// <summary>Creates a frame snapshot; the render thread is the only caller.</summary>
    internal FrameContext(IRenderPipeline? pipeline, ulong frameIndex)
    {
        Pipeline = pipeline;
        FrameIndex = frameIndex;
    }
}

/// <summary>Execution context handed to every command executor.</summary>
public ref struct CommandContext
{
    /// <summary>Device that executes the recorded commands.</summary>
    public IRenderDevice Device { get; internal set; }

    /// <summary>Device seen as RHI v2, or <c>null</c> when the backend implements only v1.</summary>
    public IRenderDeviceV2? DeviceV2 { get; internal set; }

    /// <summary>Creates the execution context.</summary>
    internal CommandContext(IRenderDevice device)
    {
        Device = device;
        DeviceV2 = device as IRenderDeviceV2;
    }
}
