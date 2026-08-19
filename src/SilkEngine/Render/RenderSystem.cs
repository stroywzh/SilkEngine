using System;
using System.Linq;
using SilkEngine.Core;
using SilkEngine.Threading;

namespace SilkEngine.Render;

public sealed class RenderSystem : IDisposable
{
    private readonly IRenderBackend _backend;
    private readonly ThreadManager _threadManager;
    private readonly RenderThreadLoop _renderThread;
    private readonly RenderCollector _collector = new();
    private IRenderPipeline _pipeline;

    public RenderSystem(
        IRenderBackend backend,
        ThreadManager threadManager,
        IRenderPipeline? pipeline = null
    )
    {
        _backend = backend;
        _threadManager = threadManager;

        var executor = _threadManager.Request<ILoopExecutor>(
            new ThreadRequest("RenderThread", ThreadKind.Dedicated)
        );
        _renderThread = new RenderThreadLoop(backend, executor);
        _pipeline = pipeline ?? new ForwardPipeline();

        Services.Register(this);
    }

    public IRenderBackend Backend => _backend;
    public bool ShouldClose => _renderThread.ShouldClose;

    public ThreadContext RenderThreadContext => _renderThread.ThreadLoop.Context;

    /// <summary>从 ThreadManager 申请专用线程执行者并启动渲染循环（本类不持有线程）。</summary>
    public void Initialize()
    {
        _renderThread.Initialize();
    }

    public void PumpEvents() => _renderThread.PumpEvents();

    public void Render(FrameSnapshot snapshot)
    {
        _collector.Gather(snapshot, out var camera, out var batches);
        camera.UpdateMatrices((float)_backend.Width / _backend.Height);
        var passes = _pipeline.Build(camera, batches);
        _renderThread.SubmitFrame(passes);
    }

    public void Dispose() => _renderThread.Dispose();
}
