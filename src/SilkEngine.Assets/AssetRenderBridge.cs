using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Assets;

/// <summary>
/// 渲染请求接收器：Assets 侧向 Rendering 提交创建/释放请求的唯一边界。
/// 只允许提交 <see cref="SilkEngine.Rendering.Abstraction"/> 的无资产语义 request/handle，
/// 不得写入 AssetId、AssetEntry 或 Payload 状态。
/// </summary>
internal interface IRenderRequestSink
{
    /// <summary>提交 GPU 资源创建请求</summary>
    /// <param name="request">创建请求（无资产语义）</param>
    void Submit(RenderResourceCreateRequest request);

    /// <summary>提交 GPU 资源释放请求</summary>
    /// <param name="request">释放请求（种类 + 句柄）</param>
    void Submit(RenderResourceReleaseRequest request);
}

/// <summary>
/// 资产到渲染契约桥：将 Assets 侧 Payload 转换为 Rendering.Abstraction 的无资产语义创建请求。
/// 本类与 Rendering 域之间只交换 request/handle，不解析资产类型、不查询 AssetManager。
/// </summary>
internal sealed class AssetRenderBridge
{
    private readonly IRenderRequestSink _sink;

    /// <summary>创建资产渲染桥</summary>
    /// <param name="sink">渲染请求接收器</param>
    internal AssetRenderBridge(IRenderRequestSink sink) => _sink = sink;

    /// <summary>纹理载荷 → 无资产语义纹理创建请求</summary>
    /// <param name="payload">纹理载荷</param>
    /// <returns>创建请求</returns>
    internal RenderTextureCreateRequest CreateTextureRequest(TextureAsset payload) => new(
        new RenderTextureDescriptor(payload.Data.Width, payload.Data.Height, 4),
        payload.Data.RawBytes);

    /// <summary>着色器载荷 → 无资产语义着色器创建请求（GL 双源码时代占位：同源注入顶点/片段）</summary>
    /// <param name="payload">着色器载荷</param>
    /// <returns>创建请求</returns>
    internal RenderShaderCreateRequest CreateShaderRequest(ShaderAsset payload) => new(
        // TODO(task 7): ShaderCompileRequest 接入（单 HLSL 源码 + 入口编译）
        new RenderShaderDescriptor(payload.Source, payload.Source));

    /// <summary>网格载荷 → 无资产语义网格创建请求</summary>
    /// <param name="payload">网格载荷</param>
    /// <returns>创建请求</returns>
    internal RenderMeshCreateRequest CreateMeshRequest(MeshAsset payload) => new(
        new RenderMeshDescriptor(payload.Vertices.Length, payload.Indices?.Length ?? 0, payload.Layout),
        payload.Vertices,
        payload.Indices ?? []);

    /// <summary>提交纹理创建请求到接收器</summary>
    /// <param name="payload">纹理载荷</param>
    internal void SubmitTexture(TextureAsset payload) => _sink.Submit(CreateTextureRequest(payload));

    /// <summary>提交着色器创建请求到接收器</summary>
    /// <param name="payload">着色器载荷</param>
    internal void SubmitShader(ShaderAsset payload) => _sink.Submit(CreateShaderRequest(payload));

    /// <summary>提交网格创建请求到接收器</summary>
    /// <param name="payload">网格载荷</param>
    internal void SubmitMesh(MeshAsset payload) => _sink.Submit(CreateMeshRequest(payload));
}
