using SilkEngine.Assets;
using SilkEngine.Core;
using SilkEngine.Scene;

namespace SandBox.Demos;

public static class TestNDCTriangle
{
    public static void Run(EngineLoop engine)
    {
        var scene = new Scene("NDC_Triangle");
        engine.SceneManager.LoadScene(scene);

        var shader = new ShaderAsset("NDC", ShaderSources.NdcColorVertex, ShaderSources.NdcColorFragment);
        var mesh = new MeshAsset(
            "Triangle",
            [
                -0.5f, -0.5f, 0, 1, 0, 0,
                0.5f, -0.5f, 0, 0, 1, 0,
                0.0f, 0.5f, 0, 0, 0, 1,
            ],
            [3, 3],
            null);

        var go = new GameObject("TriangleObj");
        var mr = go.AddComponent<MeshRenderer>();
        mr.SetShader(new AssetHandle<ShaderAsset>(DemoAssets.NewId()));
        mr.SetMesh(new AssetHandle<MeshAsset>(DemoAssets.NewId()));
        // scene.AddRootObject(go);
        engine.SceneManager.AddObjectToScene(go);

        Log.Info($"[TestNDCTriangle] {shader.Name} + {mesh.Name} 已装配（GPU 句柄待创建请求接线后发布）");
    }
}
