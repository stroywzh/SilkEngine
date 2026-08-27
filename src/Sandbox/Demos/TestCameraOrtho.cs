using SilkEngine.Assets;
using SilkEngine.Core;
using SilkEngine.Math;
using SilkEngine.Render;
using SilkEngine.Scene;

namespace SandBox.Demos;

public static class TestCameraOrtho
{
    public static void Run(EngineLoop engine)
    {
        var scene = new Scene("Camera_Ortho");
        engine.SceneManager.LoadScene(scene);

        var shader = new ShaderAsset("Cam", ShaderSources.CamUvVertex, ShaderSources.CamUvFragment);
        var mesh = DemoAssets.MeshFrom(MeshFactory.CreateQuad(1, 1));

        var quad = new GameObject("Quad");
        quad.Transform.LocalScale = new Vector3(4, 3, 1);
        var mr = quad.AddComponent<MeshRenderer>();
        mr.SetShader(new AssetHandle<ShaderAsset>(DemoAssets.NewId()));
        mr.SetMesh(new AssetHandle<MeshAsset>(DemoAssets.NewId()));
        scene.AddRootObject(quad);

        Log.Info($"[TestCameraOrtho] {shader.Name} + {mesh.Name} 已装配（GPU 句柄待创建请求接线后发布）");

        var camObj = new GameObject("Cam");
        camObj.Transform.LocalPosition = new Vector3(0, 0, -1);
        var cam = camObj.AddComponent<Camera>();
        cam.Orthographic = true;
        cam.OrthographicSize = 5f;
        scene.AddRootObject(camObj);
    }
}
