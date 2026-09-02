using SilkEngine.Assets;
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
        // 正式磁盘资产：材质经静态 Assets 门面解析（Cube.asset 声明 shader+texture 依赖）
        ui.Material = Assets.Load<MaterialAsset>("Materials/Cube.asset").ToInstance();
        ui.Mesh = DemoAssetsExt.CreateQuadMesh(host, 1f, 1f);
        ui.Texture = Assets.GetHandle<TextureAsset>("Textures/" + PictureName);
        scene.AddRootObject(quad);
    }
}
