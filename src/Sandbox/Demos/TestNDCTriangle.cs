using SilkEngine.Assets;
using SilkEngine.Host;
using SilkEngine.Scene;

namespace SandBox.Demos;

public static class TestNDCTriangle
{
    public static void Run(EngineHost host)
    {
        var scene = new Scene("NDC_Triangle");
        host.SceneManager.LoadScene(scene);

        var shader = DemoAssetsExt.CreateShader(host, "NDC", ShaderSources.NdcColorVertex, ShaderSources.NdcColorFragment);
        var mesh = DemoAssetsExt.CreateMesh(host, new MeshAsset(
            "Triangle",
            [
                -0.5f, -0.5f, 0, 1, 0, 0,
                0.5f, -0.5f, 0, 0, 1, 0,
                0.0f, 0.5f, 0, 0, 0, 1,
            ],
            [3, 3],
            null));

        var go = new GameObject("TriangleObj");
        var mr = go.AddComponent<MeshRenderer>();
        mr.Shader = shader;
        mr.Mesh = mesh;
        host.SceneManager.AddObjectToScene(go);
    }
}
