using System;
using System.Linq;

namespace SilkEngine.Render;

public sealed class RenderSystem : IDisposable
{
    private readonly IRenderBackend _backend;
    private readonly RenderCollector _collector = new();
    private IRenderPipeline _pipeline;

    public RenderSystem(IRenderBackend backend, IRenderPipeline? pipeline = null)
    {
        _backend = backend;
        _pipeline = pipeline ?? new ForwardPipeline();
    }

    public void Render(FrameSnapshot snapshot)
    {
        _collector.Gather(snapshot, out var camera, out var batches);
        camera.UpdateMatrices((float)_backend.Width / _backend.Height);
        var passes = _pipeline.Build(camera, batches);

        foreach (var pass in passes.OrderBy(p => p.SortOrder))
        {
            pass.BeforeCommands?.Invoke(_backend);
            _backend.ExecutePass(pass.Commands);
            pass.AfterCommands?.Invoke(_backend);
        }
        _backend.Present();
    }

    public void Dispose() { }
}
