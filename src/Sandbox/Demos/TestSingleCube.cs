using SilkEngine.Assets;
using SilkEngine.Core;
using SilkEngine.Math;
using SilkEngine.Render;
using SilkEngine.Scene;

namespace SandBox.Demos;

public static class TestSingleCube
{
    public static void Run(EngineLoop engine)
    {
        var scene = new Scene("SingleCube");
        engine.SceneManager.LoadScene(scene);

        var shader = new ShaderAsset("PerspCheck", ShaderSources.LitVertex, ShaderSources.LitFragment);
        var mesh = MeshFactory.CreateCube(1f);

        var cube = new GameObject("Cube");
        var mr = cube.AddComponent<MeshRenderer>();
        mr.SetShader(new AssetHandle<ShaderAsset>(DemoAssets.NewId()));
        mr.SetMesh(new AssetHandle<MeshAsset>(DemoAssets.NewId()));
        scene.AddRootObject(cube);

        Log.Info($"[TestSingleCube] {shader.Name} + {mesh.Name} 已装配（GPU 句柄待创建请求接线后发布）");

        var camObj = new GameObject("Cam");
        camObj.Transform.LocalPosition = new Vector3(3, 2, -5);
        var cam = camObj.AddComponent<Camera>();
        scene.AddRootObject(camObj);
    }
}
