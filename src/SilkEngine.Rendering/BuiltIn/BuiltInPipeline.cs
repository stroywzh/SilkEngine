using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.Pipeline;

public sealed class BuiltInPipeline : IRenderPipeline
{
    public string Name => "Built-In Pipeline";
    private IRenderBackend _renderBackend;
    private List<BuiltInCommandBuffer> _commandBuffers = new();

    public void Initialize(IRenderBackend backend)
    {
        _renderBackend = backend;
    }

    public void Resize(int width, int height)
    {
        throw new NotImplementedException();
    }

    public ICommandBuffer BeginFrame()
    {
        throw new NotImplementedException();
    }

    public ICommandBuffer CreateCommandBuffer()
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        foreach(var cb in _commandBuffers)
        {
            cb.Dispose();
        }
    }

    public void EndFrame(ICommandBuffer commandBuffer)
    {
        throw new NotImplementedException();
    }

    public void RenderFrame(RenderContext ctx)
    {
        foreach(var cb in _commandBuffers)
        {
            cb.Execute(ctx);
        }
    }
}
