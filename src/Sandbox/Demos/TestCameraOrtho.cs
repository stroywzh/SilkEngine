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

        var shader = new Shader
        {
            Name = "Cam",
            VertexSource = ShaderSources.CamUvVertex,
            FragmentSource = ShaderSources.CamUvFragment,
        };

        var mat = new Material { Name = "Mat" };
        var quad = new GameObject("Quad");
        quad.Transform.LocalScale = new Vector3(4, 3, 1);
        var mr = quad.AddComponent<MeshRenderer>();
        mr.Shader = shader;
        mr.Mesh = MeshFactory.CreateQuad(1, 1);
        mr.Material = mat;
        scene.AddRootObject(quad);

        var camObj = new GameObject("Cam");
        camObj.Transform.LocalPosition = new Vector3(0, 0, -1);
        var cam = camObj.AddComponent<Camera>();
        cam.Orthographic = true;
        cam.OrthographicSize = 5f;
        scene.AddRootObject(camObj);
    }
}
