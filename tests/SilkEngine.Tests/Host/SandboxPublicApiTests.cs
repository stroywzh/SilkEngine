using System;
using System.IO;
using System.Linq;
using SilkEngine.Host;

namespace SilkEngine.Tests.Host;

/// <summary>
/// Sandbox public-only 启动边界：Program.cs 只经 EngineHost 公共 API 启动；
/// Builder 选项经 Create(configure) 可配置且宿主可见。
/// 与 EngineHostTests 同集合串行（Headless Initialize 装配真实对象图并自注册、Dispose 触发 Services.Shutdown）。
/// </summary>
[Collection("Assets")]
public class SandboxPublicApiTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    private static string FindSource(string relativePath)
    {
        var file = Path.Combine(RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(file) ? file : throw new FileNotFoundException($"{relativePath} 未找到");
    }

    private static string ReadDemoSources()
    {
        var demos = Path.Combine(RepoRoot, "src", "Sandbox", "Demos");
        return string.Join(
            "\n",
            Directory.EnumerateFiles(demos, "*.cs", SearchOption.TopDirectoryOnly)
                .Select(File.ReadAllText));
    }

    [Fact]
    public void SandboxSource_UsesHostUsesStaticFacadeAndCleanResourcesPath()
    {
        var source = File.ReadAllText(FindSource("src/Sandbox/Program.cs"));
        var demoSources = ReadDemoSources();

        Assert.Contains("EngineHost.Create", source);
        Assert.Contains("UseAssetRoot(\"Assets\")", source);
        Assert.DoesNotContain("new OpenGLRenderBackend", source);
        Assert.DoesNotContain("EngineLoop", source);
        Assert.DoesNotContain("Services", source);
        Assert.DoesNotContain("ThreadRuntime", source);

        // 正式展示路径经静态 Assets 门面访问磁盘资源，不再残留 Resources/ 瞬态路径
        Assert.Contains("Assets.Load", demoSources);
        Assert.DoesNotContain("Resources/", demoSources);
        Assert.DoesNotContain("Resources\\", demoSources);
    }

    [Fact]
    public void SandboxSource_DoesNotReferenceRenderMachineryOrAssetDatabase()
    {
        var program = File.ReadAllText(FindSource("src/Sandbox/Program.cs"));
        var gameplay = File.ReadAllText(FindSource("src/Sandbox/Gameplay.cs"));
        var sources = string.Join("\n", program, gameplay, ReadDemoSources());

        // Rendering 域机制（OpenGL 后端/渲染线程/渲染域命名空间）与 AssetDB 类型名
        // 不得直接出现在 Sandbox 源码：业务只经 Host 公开 API 与静态门面消费
        Assert.DoesNotContain("SilkEngine.Rendering", sources, StringComparison.Ordinal);
        Assert.DoesNotContain("RenderThreadHost", sources, StringComparison.Ordinal);
        Assert.DoesNotContain("SqliteAssetDatabase", sources, StringComparison.Ordinal);
        Assert.DoesNotContain("IAssetDatabase", sources, StringComparison.Ordinal);
    }

    [Fact]
    public void Builder_StoresBackendAssetRootAndExtensionRegistrations()
    {
        var host = EngineHost.Create(builder =>
        {
            builder.UseHeadlessForTests();
            builder.UseAssetRoot("GameAssets");
        });

        Assert.Equal("GameAssets", host.Options.AssetRoot);
        Assert.True(host.Options.Headless);
    }

    [Fact]
    public void Host_HeadlessInitialize_AssemblesRuntimeGraph()
    {
        using var host = EngineHost.Create(b => b.UseHeadlessForTests());
        host.Initialize();

        Assert.True(host.IsInitialized);
        Assert.NotNull(host.SceneManager);
        Assert.NotNull(host.AssetManager);
    }
}