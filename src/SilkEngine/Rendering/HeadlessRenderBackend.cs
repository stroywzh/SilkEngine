using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.Backend;

namespace SilkEngine.Rendering;

/// <summary>
/// 无头渲染后端（测试装配专用）：不创建窗口与 GL 上下文，Execute/Present/Release 均为安全 no-op。
/// 仅由 EngineHost 在 <c>Headless=true</c> 时选择；业务代码不可见（internal）。
/// </summary>
internal sealed class HeadlessRenderBackend : IRenderBackend
{
    private ulong _nextHandle = 1;

    /// <inheritdoc />
    public void Initialize()
    {
        // 无窗口：无上下文可初始化
    }

    /// <inheritdoc />
    public RenderTextureHandle CreateTexture(RenderTextureCreateRequest request) => new(_nextHandle++);

    /// <inheritdoc />
    public RenderShaderHandle CreateShader(RenderShaderCreateRequest request) => new(_nextHandle++);

    /// <inheritdoc />
    public RenderMeshHandle CreateMesh(RenderMeshCreateRequest request) => new(_nextHandle++);

    /// <inheritdoc />
    public void Execute(RenderPacket packet)
    {
        // 无头模式：不执行真实绘制
    }

    /// <inheritdoc />
    public void Present()
    {
        // 无窗口：无交换缓冲
    }

    /// <inheritdoc />
    public void Release(RenderResourceReleaseRequest request)
    {
        // 无 GPU 资源可释放
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // 无资源
    }
}