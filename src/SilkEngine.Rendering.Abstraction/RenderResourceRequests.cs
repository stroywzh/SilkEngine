namespace SilkEngine.Rendering.Abstraction;

/// <summary>纹理创建描述：宽、高与通道数。</summary>
public sealed record RenderTextureDescriptor(int Width, int Height, int Channels);

/// <summary>着色器创建描述：顶点与片元着色器源码（字符串不可变，无需复制）。</summary>
public sealed record RenderShaderDescriptor(string VertexSource, string FragmentSource);

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

/// <summary>着色器创建请求。</summary>
public sealed record RenderShaderCreateRequest(
    RenderShaderDescriptor Descriptor) : RenderResourceCreateRequest(RenderResourceKind.Shader);

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
