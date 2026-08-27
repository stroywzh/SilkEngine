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

        var shader = new Shader
        {
            Name = "Persp",
            VertexSource = ShaderSources.LitVertex,
            FragmentSource = ShaderSources.LitFragment,
        };

        var mat = new MaterialLegacy { Name = "Mat" };
        var cube = new GameObject("Cube");
        cube.Transform.LocalPosition = new Vector3(0, 0, 3);
        var mr = cube.AddComponent<MeshRenderer>();
        mr.Shader = shader;
        mr.Mesh = MeshFactory.CreateCube(1f);
        mr.Material = mat;
        scene.AddRootObject(cube);

        var camObj = new GameObject("Cam");
        camObj.Transform.LocalPosition = new Vector3(0, 0, -2);
        var cam = camObj.AddComponent<Camera>();
        cam.Orthographic = false;
        scene.AddRootObject(camObj);
    }
}
