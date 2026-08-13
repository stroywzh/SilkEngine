using SilkEngine.Core;
using SilkEngine.Math;
using SilkEngine.Render;
using SilkEngine.Scene;

namespace SandBox.Demos;

public static class TestNDCTriangle
{
    public static void Run(EngineLoop engine)
    {
        var scene = new Scene("NDC_Triangle");
        engine.SceneManager.LoadScene(scene);

        var shader = new Shader
        {
            Name = "NDC",
            VertexSource = ShaderSources.NdcColorVertex,
            FragmentSource = ShaderSources.NdcColorFragment,
        };

        var mesh = new Mesh
        {
            Name = "Triangle",
            Vertices = new float[]
            {
                -0.5f,
                -0.5f,
                0,
                1,
                0,
                0,
                0.5f,
                -0.5f,
                0,
                0,
                1,
                0,
                0.0f,
                0.5f,
                0,
                0,
                0,
                1,
            },
            Layout = new[] { 3, 3 },
        };

        var go = new GameObject("TriangleObj");
        var mr = go.AddComponent<MeshRenderer>();

        mr.Shader = shader;
        mr.Mesh = mesh;
        // scene.AddRootObject(go);
        engine.SceneManager.AddObjectToScene(go);
    }
}
