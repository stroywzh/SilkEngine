using SilkEngine.Host;
using SilkEngine.Math;
using SilkEngine.Scene;

namespace SandBox.Demos;

public static class TestCameraPerspective
{
    public static void Run(EngineHost host)
    {
        var scene = new Scene("Camera_Persp");
        host.SceneManager.LoadScene(scene);

        var cube = new GameObject("Cube");
        cube.Transform.LocalPosition = new Vector3(0, 0, 3);
        var mr = cube.AddComponent<MeshRenderer>();
        // TODO(task 11): 重写为 Assets.Load + 真实 HLSL 资产
        mr.Shader = DemoAssetsExt.CreateShader(host, "Persp", ShaderSources.LitVertex);
        mr.Mesh = DemoAssetsExt.CreateCubeMesh(host);
        scene.AddRootObject(cube);

        var camObj = new GameObject("Cam");
        camObj.Transform.LocalPosition = new Vector3(0, 0, -2);
        var cam = camObj.AddComponent<Camera>();
        cam.Orthographic = false;
        scene.AddRootObject(camObj);
        cam.UpdateMatrices(16f / 9f);
    }
}
