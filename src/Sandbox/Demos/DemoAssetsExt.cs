using SilkEngine.Assets;
using SilkEngine.Host;
using SilkEngine.Render;

namespace SandBox.Demos;

/// <summary>
/// Sandbox 业务适配：仅经 Engine public API（EngineHost.AssetManager）构造瞬态资产 Handle，
/// 不直接造随机 ID，不让 Sandbox 触达内部渲染接口。
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
        => host.AssetManager.RegisterTransient(
            new ShaderAsset("PerspCheck", ShaderSources.LitVertex, ShaderSources.LitFragment));
}