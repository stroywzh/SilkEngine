using SilkEngine.Assets;
using SilkEngine.Core;
using SilkEngine.Render;
using SilkEngine.Scene;

namespace SandBox.Demos;

public static class TestNDCQuad
{
    public static void Run(EngineLoop engine)
    {
        var scene = new Scene("NDC_Quad");
        engine.SceneManager.LoadScene(scene);

        var shader = new ShaderAsset("NDC_Quad", ShaderSources.NdcUvVertex, ShaderSources.NdcUvFragment);
        var mesh = DemoAssets.MeshFrom(MeshFactory.CreateQuad(1.6f, 1.2f));

        var go = new GameObject("QuadObj");
        var mr = go.AddComponent<MeshRenderer>();
        mr.SetShader(new AssetHandle<ShaderAsset>(DemoAssets.NewId()));
        mr.SetMesh(new AssetHandle<MeshAsset>(DemoAssets.NewId()));
        scene.AddRootObject(go);

        Log.Info($"[TestNDCQuad] {shader.Name} + {mesh.Name} 已装配（GPU 句柄待创建请求接线后发布）");
    }
}
