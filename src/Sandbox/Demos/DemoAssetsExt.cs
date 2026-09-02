using SilkEngine.Assets;
using SilkEngine.Host;
using SilkEngine.Render;

namespace SandBox.Demos;

/// <summary>
/// Sandbox 业务适配：仅经 Engine public API（EngineHost.AssetManager）构造程序化瞬态资产 Handle，
/// 不直接造随机 ID，不让 Sandbox 触达内部渲染接口。
/// 正式磁盘资源（Cube/PNG）展示路径改用静态 <see cref="Assets"/> 门面，不经本辅助类。
/// </summary>
public static class DemoAssetsExt
{
    /// <summary>程序生成不透明立方网格瞬态资产 Handle。</summary>
    /// <param name="host">引擎宿主</param>
    /// <returns>网格资产句柄</returns>
    public static AssetHandle<MeshAsset> CreateCubeMesh(EngineHost host)
        => host.AssetManager.RegisterTransient(MeshFactory.CreateCube(1f));

    /// <summary>程序生成 Lit 着色器瞬态资产 Handle（uModel/uView/uProjection + 法线色）。</summary>
    /// <param name="host">引擎宿主</param>
    /// <returns>着色器资产句柄</returns>
    public static AssetHandle<ShaderAsset> CreateLitShader(EngineHost host)
        => host.AssetManager.RegisterTransient(new ShaderAsset("PerspCheck", ShaderSources.LitVertex));

    /// <summary>程序生成 Lit 材质运行时实例（着色器 + 空默认参数；顶点/片段源拼接为单源 ShaderAsset）。</summary>
    /// <param name="host">引擎宿主</param>
    /// <returns>材质运行时实例</returns>
    public static Material CreateLitMaterial(EngineHost host)
        => CreateMaterial(host, "Lit", ShaderSources.LitVertex, ShaderSources.LitFragment);

    /// <summary>程序生成材质运行时实例（瞬态 ShaderAsset → 瞬态 MaterialAsset → 独立实例）。</summary>
    /// <param name="host">引擎宿主</param>
    /// <param name="name">材质/着色器名称</param>
    /// <param name="vertex">顶点着色器源</param>
    /// <param name="fragment">片段着色器源</param>
    /// <returns>材质运行时实例</returns>
    public static Material CreateMaterial(EngineHost host, string name, string vertex, string fragment)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(vertex);
        ArgumentNullException.ThrowIfNull(fragment);
        var shader = CreateShader(host, name, string.Concat(vertex, "\n", fragment));
        var asset = new MaterialAsset(
            "Material",
            default,
            shader,
            null,
            new MaterialParameterSnapshot([]));
        var handle = host.AssetManager.RegisterTransient(asset);
        return new Material(new MaterialReference(handle.Id));
    }

    /// <summary>按名称与源码构造着色器瞬态资产 Handle。</summary>
    /// <param name="host">引擎宿主</param>
    /// <param name="name">资产名</param>
    /// <param name="source">着色器源码（单 HLSL 源码形态占位，GLSL 双源码时代遗留）</param>
    /// <returns>着色器资产句柄</returns>
    public static AssetHandle<ShaderAsset> CreateShader(EngineHost host, string name, string source)
        => host.AssetManager.RegisterTransient(new ShaderAsset(name, source));

    /// <summary>把给定网格载荷登记为瞬态资产并返回句柄。</summary>
    /// <param name="host">引擎宿主</param>
    /// <param name="mesh">网格载荷</param>
    /// <returns>网格资产句柄</returns>
    public static AssetHandle<MeshAsset> CreateMesh(EngineHost host, MeshAsset mesh)
        => host.AssetManager.RegisterTransient(mesh);

    /// <summary>程序生成 XY 平面四边形网格瞬态资产 Handle。</summary>
    /// <param name="host">引擎宿主</param>
    /// <param name="width">宽</param>
    /// <param name="height">高</param>
    /// <returns>网格资产句柄</returns>
    public static AssetHandle<MeshAsset> CreateQuadMesh(EngineHost host, float width, float height)
        => host.AssetManager.RegisterTransient(MeshFactory.CreateQuad(width, height));
}
