using SilkEngine.Assets;
using SilkEngine.Core;
using SilkEngine.Render;
using SilkEngine.Scene;

namespace SandBox.Demos;

public static class IMGShow
{
    private static readonly string PictureName = "ShoreKeeper1.png";

    public static void Run(EngineLoop engine)
    {
        var scene = new Scene("IMGShow_DEBUG");
        engine.SceneManager.LoadScene(scene);

        var shader = new Shader
        {
            Name = "PngShader",
            VertexSource = ShaderSources.PngVertex,
            FragmentSource = ShaderSources.PngFragment,
        };

        string path = Path.Combine(AppContext.BaseDirectory, "Resources", PictureName);
        var tex = engine.AssetManager.Load<Texture2D>(path);

        var mat = new Material { Name = "PngMat" };
        mat.MainTexture = tex;

        var quad = new GameObject("UIRenderQuad");
        var ui = quad.AddComponent<UIRenderer>();
        ui.Shader = shader;
        ui.Mesh = MeshFactory.CreateQuad(1f, 1f);
        ui.Material = mat;
        scene.AddRootObject(quad);

        Log.Info(
            $"[IMGShow] UIRenderer assembled: Shader='{ui.Shader?.Name}' " +
            $"Mesh='{ui.Mesh?.Name}' Material='{ui.Material?.Name}' Texture={tex.Data.Width}x{tex.Data.Height}"
        );
    }
}
