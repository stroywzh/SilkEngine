using SilkEngine.Assets;
using SilkEngine.Core;
using SilkEngine.Math;
using SilkEngine.Render;
using SilkEngine.Scene;

namespace SandBox.Demos;

public static class TestPNGQuad
{
    public static void Run(EngineLoop engine)
    {
        var scene = new Scene("PNG_Quad");
        engine.SceneManager.LoadScene(scene);

        var shader = new Shader
        {
            Name = "PNG",
            VertexSource = ShaderSources.PngVertex,
            FragmentSource = ShaderSources.PngFragment,
        };

        var mat = new Material(new MaterialReference(new AssetId(Guid.NewGuid())));
        var quad = new GameObject("PNGQuad");
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
