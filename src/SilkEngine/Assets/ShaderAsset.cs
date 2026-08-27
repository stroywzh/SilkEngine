namespace SilkEngine.Assets;

/// <summary>着色器资产载荷：纯文本源码容器（GL 编译由渲染后端完成）</summary>
/// <param name="name">着色器名称</param>
/// <param name="vertexSource">顶点着色器 GLSL 源码</param>
/// <param name="fragmentSource">片段着色器 GLSL 源码</param>
public sealed class ShaderAsset(string name, string vertexSource, string fragmentSource) : IAssetPayload
{
    /// <summary>着色器名称</summary>
    public string Name { get; } = name;

    /// <summary>顶点着色器 GLSL 源码</summary>
    public string VertexSource { get; } = vertexSource;

    /// <summary>片段着色器 GLSL 源码</summary>
    public string FragmentSource { get; } = fragmentSource;
}
