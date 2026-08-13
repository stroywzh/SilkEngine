using SilkEngine.Core;
using SilkEngine.Core.Assets;
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

        var mat = new Material { Name = "PNGMat" };
        var quad = new GameObject("PNGQuad");
        quad.Transform.LocalScale = new Vector3(4, 3, 1);
        var mr = quad.AddComponent<MeshRenderer>();
        mr.Shader = shader;
        mr.Mesh = MeshFactory.CreateQuad(1, 1);
        mr.Material = mat;
        var demo = quad.AddComponent<TextureDemoRunner>();
        demo.Target = mat;
        demo.Engine = engine;
        scene.AddRootObject(quad);

        var camObj = new GameObject("Cam");
        camObj.Transform.LocalPosition = new Vector3(0, 0, -1);
        var cam = camObj.AddComponent<Camera>();
        cam.Orthographic = true;
        cam.OrthographicSize = 5f;
        scene.AddRootObject(camObj);
    }

    // LazyAsync 加载 demo 纹理：未就绪时材质无主纹理 → 白色占位，就绪后贴图
    private class TextureDemoRunner : MonoBehaviour
    {
        public EngineLoop? Engine;
        public Material? Target;
        private AssetRequest<Texture2D>? _request;

        public override void OnStart()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Resources", "test.png");
            _request = Engine!.AssetManager.LoadAsync<Texture2D>(path, AsyncLoadMode.LazyAsync);
            _ = _request.Asset; // LazyAsync 首次访问即触发加载
        }

        public override void OnUpdate(float deltaTime)
        {
            if (_request is { IsDone: true, Asset: { } tex } && Target is { MainTexture: null })
            {
                Target.MainTexture = tex;
                Log.Info("[Demo] PNG texture applied");
            }
        }
    }
}
