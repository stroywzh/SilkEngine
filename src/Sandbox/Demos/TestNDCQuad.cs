using SilkEngine.Core;
using SilkEngine.Math;
using SilkEngine.Render;
using SilkEngine.Scene;

namespace SandBox.Demos;

public static class TestNDCQuad
{
    public static void Run(EngineLoop engine)
    {
        var scene = new Scene("NDC_Quad");
        engine.SceneManager.LoadScene(scene);

        var shader = new Shader
        {
            Name = "NDC_Quad",
            VertexSource = ShaderSources.NdcUvVertex,
            FragmentSource = ShaderSources.NdcUvFragment,
        };

        var mesh = MeshFactory.CreateQuad(1.6f, 1.2f);
        var go = new GameObject("QuadObj");
        var mr = go.AddComponent<MeshRenderer>();
        mr.Shader = shader;
        mr.Mesh = mesh;
        scene.AddRootObject(go);
    }
}
