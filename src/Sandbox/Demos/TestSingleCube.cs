using SilkEngine.Host;
using SilkEngine.Math;
using SilkEngine.Scene;

namespace SandBox.Demos;

public static class TestSingleCube
{
    public static void Run(EngineHost host)
    {
        var scene = new Scene("SingleCube");
        host.SceneManager.LoadScene(scene);

        var cube = new GameObject("Cube");
        var mr = cube.AddComponent<MeshRenderer>();
        mr.Material = DemoAssetsExt.CreateLitMaterial(host);
        mr.Mesh = DemoAssetsExt.CreateCubeMesh(host);
        scene.AddRootObject(cube);

        var camObj = new GameObject("Cam");
        camObj.Transform.LocalPosition = new Vector3(3, 2, -5);
        var cam = camObj.AddComponent<Camera>();
        scene.AddRootObject(camObj);
        cam.UpdateMatrices(16f / 9f);
    }
}
