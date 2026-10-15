namespace SilkEngine.Runtime.Rendering.Abstraction;


//*
// 提交路径：
// Sence(Renders)-->RenderItems
// -------------------Pipeline内外分界线------------
// ->RenderCommandQueue → RenderFrameContext → IRenderPipeline.RenderFrame → ICommandBuffer.DrawMesh(meshId, materialId) → IRenderBackend
// 
// 渲染路径：
// MainLoop.Run()->RenderLoop.Run()->IRenderPipeline.RenderFrame，后面同上
// 
//*
public interface IRenderBackend : IDisposable
{
    string Name { get; }
    void Initialize(IGraphicsDevice device);
    void Resize(int width, int height);
    ICommandBuffer BeginFrame();
    void EndFrame(ICommandBuffer commandBuffer);
    ICommandBuffer CreateCommandBuffer();
    void WaitIdle();
}

// 目前是标记接口，因为我记得Silk.NET有一个Device啥的东西
public interface IGraphicsDevice
{
    //
}
