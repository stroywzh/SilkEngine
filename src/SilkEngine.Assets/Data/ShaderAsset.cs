using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Assets;

/// <summary>着色器资产载荷：不可变 HLSL 单源码容器（编译由渲染后端/任务 7 编译管线完成）</summary>
/// <param name="name">着色器名称</param>
/// <param name="source">HLSL 源码原文（不可变）</param>
/// <param name="vertexEntryPoint">顶点着色器入口函数名</param>
/// <param name="fragmentEntryPoint">片段着色器入口函数名</param>
/// <param name="profile">着色模型配置文件</param>
public sealed class ShaderAsset(
    string name,
    string source,
    string vertexEntryPoint = "vert",
    string fragmentEntryPoint = "frag",
    string profile = "sm_6_0") : IAssetPayload, IShader
{
    /// <summary>着色器名称</summary>
    public string Name { get; } = name;

    /// <summary>HLSL 源码（不可变原文）</summary>
    public string Source { get; } = source;

    /// <summary>顶点着色器入口函数名</summary>
    public string VertexEntryPoint { get; } = vertexEntryPoint;

    /// <summary>片段着色器入口函数名</summary>
    public string FragmentEntryPoint { get; } = fragmentEntryPoint;

    /// <summary>着色模型配置文件（如 "sm_6_0"）</summary>
    public string Profile { get; } = profile;
}