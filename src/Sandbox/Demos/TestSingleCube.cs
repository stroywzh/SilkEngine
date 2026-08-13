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

        var shader = new Shader
        {
            Name = "PerspCheck",
            VertexSource = ShaderSources.LitVertex,
            FragmentSource = ShaderSources.LitFragment,
        };

        var cube = new GameObject("Cube");
        var mr = cube.AddComponent<MeshRenderer>();
        mr.Shader = shader;
        mr.Mesh = MeshFactory.CreateCube(1f);
        scene.AddRootObject(cube);

        var camObj = new GameObject("Cam");
        camObj.Transform.LocalPosition = new Vector3(3, 2, -5);
        var cam = camObj.AddComponent<Camera>();
        scene.AddRootObject(camObj);
    }
}
