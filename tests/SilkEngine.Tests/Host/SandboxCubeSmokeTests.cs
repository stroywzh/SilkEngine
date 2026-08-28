using System;
using System.IO;
using System.Linq;
using SandBox.Demos;
using SilkEngine.Host;
using SilkEngine.Scene;

namespace SilkEngine.Tests.Host;

using Scene = SilkEngine.Scene.Scene;

/// <summary>
/// Sandbox 黑盒垂直验收：Sandbox 项目源码不引用任何引擎内部入口；
/// 经 EngineHost + DemoAssetsExt 的 headless 引擎在若干帧内完成
/// RegisterTransient → GPU 创建 → 结果发布 → Renderer Handle 解析的真实闭环。
/// </summary>
[Collection("Assets")]
public class SandboxCubeSmokeTests : IDisposable
{
    private readonly EngineHost _host;

    public SandboxCubeSmokeTests()
    {
        _host = EngineHost.Create(b => b.UseHeadlessForTests());
        _host.Initialize();
    }

    public void Dispose() => _host.Dispose();

    [Fact]
    public void SandboxProject_DoesNotReferenceEngineInternals()
    {
        var files = Directory.EnumerateFiles(SandboxRoot, "*.cs", SearchOption.AllDirectories);
        var forbidden = new[]
        {
            "Services", "ThreadRuntime", "AssetPipeline",
            "RenderThreadHost", "OpenGLRenderBackend", "PublishRender",
        };
        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            foreach (var token in forbidden)
                Assert.DoesNotContain(token, source);
        }
    }

    [Fact]
    public void CubeSmoke_ProducesNonDefaultShaderAndMeshHandles()
    {
        var scene = new Scene("Cube");
        var cube = new GameObject("Cube");
        var renderer = cube.AddComponent<MeshRenderer>();
        renderer.Shader = DemoAssetsExt.CreateLitShader(_host);
        renderer.Mesh = DemoAssetsExt.CreateCubeMesh(_host);
        scene.AddRootObject(cube);
        _host.SceneManager.LoadScene(scene);

        RunFrames(_host, 4);

        Assert.NotEqual(default, renderer.ShaderHandle);
        Assert.NotEqual(default, renderer.MeshHandle);
    }

    private static void RunFrames(EngineHost host, int count)
    {
        for (var i = 0; i < count; i++)
            host.Loop.StepFrame();
    }

    private static readonly string SandboxRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../src/Sandbox"));
}