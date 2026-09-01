using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.Backend;

/// <summary>渲染后端资源能力：创建与释放 GPU 资源，返回无资产语义的句柄。</summary>
/// <remarks>仅引用 <see cref="SilkEngine.Rendering.Abstraction"/> 与 BCL，不包含 OpenGL/Vulkan 类型。</remarks>
public interface IRenderDevice
{
    /// <summary>创建纹理资源。</summary>
    /// <param name="request">纹理创建请求。</param>
    /// <returns>纹理 GPU 句柄。</returns>
    RenderTextureHandle CreateTexture(RenderTextureCreateRequest request);

    /// <summary>创建着色器资源。</summary>
    /// <param name="request">着色器创建请求。</param>
    /// <returns>着色器 GPU 句柄。</returns>
    RenderShaderHandle CreateShader(RenderShaderCreateRequest request);

    /// <summary>创建网格资源。</summary>
    /// <param name="request">网格创建请求。</param>
    /// <returns>网格 GPU 句柄。</returns>
    RenderMeshHandle CreateMesh(RenderMeshCreateRequest request);

    /// <summary>释放 GPU 资源。</summary>
    /// <param name="request">资源释放请求（仅含种类与句柄数值）。</param>
    void Release(RenderResourceReleaseRequest request);
}
