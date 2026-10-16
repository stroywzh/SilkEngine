namespace SilkEngine.Runtime.Rendering.Abstraction;

public interface IRenderPipeline : IDisposable
{
    string Name { get; }
    void Initialize(IRenderBackend backend);
    void Resize(int width, int height);
    void RenderFrame(RenderContext context);
    ICommandBuffer BeginFrame();
    void EndFrame(ICommandBuffer commandBuffer);
    ICommandBuffer CreateCommandBuffer();
}
