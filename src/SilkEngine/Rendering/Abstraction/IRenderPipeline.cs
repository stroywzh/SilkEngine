namespace SilkEngine.Rendering.Abstraction;

/// <summary>A render-thread pipeline that records and submits work without executing the backend.</summary>
public interface IRenderPipeline : IDisposable
{
    /// <summary>Gets the pipeline display name.</summary>
    string Name { get; }

    /// <summary>Initializes once using an already initialized backend owned by the host.</summary>
    void Initialize(IRenderBackend backend);

    /// <summary>Updates a non-negative drawable size; a zero dimension skips the resize hook.</summary>
    void Resize(int width, int height);

    /// <summary>Records one frame and submits the resulting buffer when it is non-empty.</summary>
    void RecordFrame(in FrameContext context);
}
