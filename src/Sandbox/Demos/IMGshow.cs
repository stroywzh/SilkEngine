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

        var shader = new ShaderAsset("PngShader", ShaderSources.PngVertex, ShaderSources.PngFragment);
        var mesh = MeshFactory.CreateQuad(1f, 1f);

        string path = Path.Combine(AppContext.BaseDirectory, "Resources", PictureName);
        var tex = engine.AssetManager.Load<TextureAsset>(path);

        var quad = new GameObject("UIRenderQuad");
        var ui = quad.AddComponent<UIRenderer>();
        ui.SetShader(new AssetHandle<ShaderAsset>(DemoAssets.NewId()));
        ui.SetMesh(new AssetHandle<MeshAsset>(DemoAssets.NewId()));
        scene.AddRootObject(quad);

        Log.Info(
            $"[IMGShow] UIRenderer assembled: Shader='{shader.Name}' Mesh='{mesh.Name}' " +
            $"Texture={tex.Data.Width}x{tex.Data.Height}（GPU 句柄待创建请求接线后发布）"
        );
    }
}
