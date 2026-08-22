using SilkEngine.Assets;

namespace SilkEngine.Render;

/// <summary>
/// 纯数据着色器容器
/// <br/>后端将其编译为 GPU 资源 (IShader)
/// </summary>
public class Shader : IAsset
{
    /// <summary>着色器标识名称</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>顶点着色器 GLSL 源码</summary>
    public string VertexSource { get; init; } = string.Empty;

    /// <summary>片段着色器 GLSL 源码</summary>
    public string FragmentSource { get; init; } = string.Empty;

    public override int GetHashCode() => Name.GetHashCode();

    public override bool Equals(object? obj) => obj is Shader s && s.Name == Name;
}
