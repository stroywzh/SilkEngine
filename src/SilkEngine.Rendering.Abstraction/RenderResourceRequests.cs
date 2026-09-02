namespace SilkEngine.Rendering.Abstraction;

/// <summary>纹理创建描述：宽、高与通道数。</summary>
public sealed record RenderTextureDescriptor(int Width, int Height, int Channels);

/// <summary>网格创建描述；顶点属性布局数组在构造时复制。</summary>
public sealed record RenderMeshDescriptor(int VertexCount, int IndexCount, int[] Layout)
{
    /// <summary>顶点属性布局（每顶点分量数）；构造时的私有副本，避免调用方后续修改。</summary>
    public int[] Layout { get; init; } = Layout.ToArray();
}

/// <summary>GPU 资源创建请求基类。</summary>
public abstract record RenderResourceCreateRequest(RenderResourceKind Kind);

/// <summary>纹理创建请求；像素数据在构造时复制为私有副本。</summary>
public sealed record RenderTextureCreateRequest(
    RenderTextureDescriptor Descriptor,
    ReadOnlyMemory<byte> PixelData) : RenderResourceCreateRequest(RenderResourceKind.Texture)
{
    /// <summary>像素数据；构造时的私有副本，避免调用方后续修改。</summary>
    public ReadOnlyMemory<byte> PixelData { get; init; } = PixelData.ToArray();
}

/// <summary>
/// 着色器创建请求（backend-neutral 编译请求）：单 HLSL 源 + 顶点/片元入口 + profile + 宏 + 后端标签。
/// GLSL 双源码时代终结——GPU 端加载路径为 SPIR-V（HLSL→SPIR-V 由 Rendering.Backend 编译器契约完成）。
/// </summary>
/// <param name="SourcePath">源着色器路径/名称（错误定位与日志）</param>
/// <param name="HlslSource">HLSL 源码原文（不可变）</param>
/// <param name="VertexEntryPoint">顶点着色器入口函数名</param>
/// <param name="FragmentEntryPoint">片元着色器入口函数名</param>
/// <param name="Profile">着色模型 profile（如 "sm_6_0"）</param>
/// <param name="Defines">编译宏定义列表</param>
/// <param name="Backend">目标后端标签（如 <see cref="ShaderBackends.OpenGl"/>）</param>
public sealed record RenderShaderCreateRequest(
    string SourcePath,
    string HlslSource,
    string VertexEntryPoint,
    string FragmentEntryPoint,
    string Profile,
    IReadOnlyList<string> Defines,
    string Backend) : RenderResourceCreateRequest(RenderResourceKind.Shader)
{
    /// <summary>编译宏定义列表；构造时复制为私有副本，避免调用方后续修改。</summary>
    public IReadOnlyList<string> Defines { get; init; } = Defines.ToArray();
}

/// <summary>后端标签常量（编译请求负载；Assets 域与 Rendering 域经此约定后端名，避免脏字符串）。</summary>
public static class ShaderBackends
{
    /// <summary>OpenGL 后端标签。</summary>
    public const string OpenGl = "opengl";
}

/// <summary>网格创建请求；顶点与索引数据在构造时复制为私有副本。</summary>
public sealed record RenderMeshCreateRequest(
    RenderMeshDescriptor Descriptor,
    ReadOnlyMemory<float> Vertices,
    ReadOnlyMemory<int> Indices) : RenderResourceCreateRequest(RenderResourceKind.Mesh)
{
    /// <summary>顶点数据；构造时的私有副本，避免调用方后续修改。</summary>
    public ReadOnlyMemory<float> Vertices { get; init; } = Vertices.ToArray();

    /// <summary>索引数据；构造时的私有副本，避免调用方后续修改。</summary>
    public ReadOnlyMemory<int> Indices { get; init; } = Indices.ToArray();
}
