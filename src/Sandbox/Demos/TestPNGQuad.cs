using SilkEngine.Assets;
using SilkEngine.Host;
using SilkEngine.Math;
using SilkEngine.Scene;

namespace SandBox.Demos;

public static class TestPNGQuad
{
    public static void Run(EngineHost host)
    {
        var scene = new Scene("PNG_Quad");
        host.SceneManager.LoadScene(scene);

        var quad = new GameObject("PNGQuad");
        quad.Transform.LocalScale = new Vector3(4, 3, 1);
        var mr = quad.AddComponent<MeshRenderer>();
        // 正式磁盘资产：材质经静态 Assets 门面解析（Cube.asset 声明 shader+texture 依赖）
        mr.Material = Assets.Load<MaterialAsset>("Materials/Cube.asset").ToInstance();
        mr.Mesh = DemoAssetsExt.CreateQuadMesh(host, 1, 1);
        mr.Texture = Assets.GetHandle<TextureAsset>("Textures/ShoreKeeper1.png");
        scene.AddRootObject(quad);

        var camObj = new GameObject("Cam");
        camObj.Transform.LocalPosition = new Vector3(0, 0, -1);
        var cam = camObj.AddComponent<Camera>();
        cam.Orthographic = true;
        cam.OrthographicSize = 5f;
        scene.AddRootObject(camObj);
        cam.UpdateMatrices(16f / 9f);
    }
}
