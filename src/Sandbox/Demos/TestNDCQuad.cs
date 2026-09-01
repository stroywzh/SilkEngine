using SilkEngine.Host;
using SilkEngine.Scene;

namespace SandBox.Demos;

public static class TestNDCQuad
{
    public static void Run(EngineHost host)
    {
        var scene = new Scene("NDC_Quad");
        host.SceneManager.LoadScene(scene);

        var go = new GameObject("QuadObj");
        var mr = go.AddComponent<MeshRenderer>();
        mr.Shader = DemoAssetsExt.CreateShader(host, "NDC_Quad", ShaderSources.NdcUvVertex, ShaderSources.NdcUvFragment);
        mr.Mesh = DemoAssetsExt.CreateQuadMesh(host, 1.6f, 1.2f);
        scene.AddRootObject(go);
    }
}
