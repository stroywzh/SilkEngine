namespace SilkEngine.Rendering.Abstraction;

public interface IRenderPipeline : IDisposable
{
    string Name { get; }
    void Initialize(IRenderBackend backend);
    void Resize(int width, int height);
    void RenderFrame(RenderContext ctx);
    ICommandBuffer BeginFrame();
    void EndFrame(ICommandBuffer commandBuffer);
    ICommandBuffer CreateCommandBuffer();
}
