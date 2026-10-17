using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering;

/// <summary>
/// Runs the fixed per-frame order: pipeline records/submits, then backend consumes. It does not own
/// or dispose either object and never calls <see cref="IRenderBackend.WaitForIdle"/>.
/// </summary>
public sealed class RenderFrameDriver : IDisposable
{
    private readonly IRenderPipeline _pipeline;
    private readonly IRenderBackend _backend;

    /// <summary>Creates a driver over an initialized pipeline and its initialized backend.</summary>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    /// <remarks>The host must supply the same backend used to initialize the pipeline and invoke
    /// this driver on the backend's owning thread. Constructors do not validate device identity.</remarks>
    public RenderFrameDriver(IRenderPipeline pipeline, IRenderBackend backend)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public void Initialize(IGraphicsDevice device)
    {
        _backend.Initialize(device);
        _pipeline.Initialize(_backend);
    }

    /// <summary>Runs one frame; an empty frame still calls <see cref="IRenderBackend.ExecuteRender"/>.</summary>
    /// <exception cref="Exception">Propagates failures; the backend is not executed when the pipeline throws.</exception>
    public void RenderFrame(RenderContext context)
    {
        _pipeline.RenderFrame(context);
        _backend.ExecuteRender();
    }

    public bool IsRenderFinished { get; private set; } = false;

    public void Dispose()
    {
        _pipeline.Dispose();
        _backend.Dispose();
    }
}
