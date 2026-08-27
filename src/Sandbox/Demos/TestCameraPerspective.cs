using SilkEngine.Assets;
using SilkEngine.Core;
using SilkEngine.Math;
using SilkEngine.Render;
using SilkEngine.Scene;

namespace SandBox.Demos;

public static class TestCameraPerspective
{
    public static void Run(EngineLoop engine)
    {
        var scene = new Scene("Camera_Persp");
        engine.SceneManager.LoadScene(scene);

        var shader = new ShaderAsset("Persp", ShaderSources.LitVertex, ShaderSources.LitFragment);
        var mesh = MeshFactory.CreateCube(1f);

        var cube = new GameObject("Cube");
        cube.Transform.LocalPosition = new Vector3(0, 0, 3);
        var mr = cube.AddComponent<MeshRenderer>();
        mr.SetShader(new AssetHandle<ShaderAsset>(DemoAssets.NewId()));
        mr.SetMesh(new AssetHandle<MeshAsset>(DemoAssets.NewId()));
        scene.AddRootObject(cube);

        Log.Info($"[TestCameraPerspective] {shader.Name} + {mesh.Name} 已装配（GPU 句柄待创建请求接线后发布）");

        var camObj = new GameObject("Cam");
        camObj.Transform.LocalPosition = new Vector3(0, 0, -2);
        var cam = camObj.AddComponent<Camera>();
        cam.Orthographic = false;
        scene.AddRootObject(camObj);
    }
}
