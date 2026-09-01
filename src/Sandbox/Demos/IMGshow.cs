using SilkEngine.Host;
using SilkEngine.Scene;

namespace SandBox.Demos;

public static class IMGShow
{
    private static readonly string PictureName = "ShoreKeeper1.png";

    public static void Run(EngineHost host)
    {
        var scene = new Scene("IMGShow_DEBUG");
        host.SceneManager.LoadScene(scene);

        var quad = new GameObject("UIRenderQuad");
        var ui = quad.AddComponent<UIRenderer>();
        ui.Shader = DemoAssetsExt.CreateShader(host, "PngShader", ShaderSources.PngVertex, ShaderSources.PngFragment);
        ui.Mesh = DemoAssetsExt.CreateQuadMesh(host, 1f, 1f);
        ui.Texture = DemoAssetsExt.CreateTexture(host, "Resources/" + PictureName);
        scene.AddRootObject(quad);
    }
}
