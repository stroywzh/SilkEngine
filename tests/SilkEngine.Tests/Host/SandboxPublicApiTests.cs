using System;
using System.IO;
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

    [Fact]
    public void SandboxSource_UsesHostAndDoesNotUseInternalAssemblyNames()
    {
        var source = File.ReadAllText(FindSource("src/Sandbox/Program.cs"));

        Assert.Contains("EngineHost.Create", source);
        Assert.DoesNotContain("new OpenGLRenderBackend", source);
        Assert.DoesNotContain("EngineLoop", source);
        Assert.DoesNotContain("Services", source);
        Assert.DoesNotContain("ThreadRuntime", source);
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