using SilkEngine.Assets;
using SilkEngine.Assets.Importer;
using SilkEngine.Assets.VirtualFileSystem;
using SilkEngine.Core;
using SilkEngine.Rendering;
using SilkEngine.Scene;
using SilkEngine.Threading;

namespace SilkEngine.Tests.Architecture;

/// <summary>
/// 阶段 4 任务 1：运行时模块构造不得改写全局服务注册表（构造器自注册已移除，Host 集中装配）；
/// 渲染器解析不得经 Services 定位（热路径无服务定位）。
/// </summary>
[Collection("Assets")]
public class ConstructorSideEffectTests
{
    private static AssetManager CreateAssetManager(ThreadRuntime runtime)
    {
        var pipeline = new AssetPipeline(
            new InMemoryAssetFileSystem("Assets"),
            new InMemoryVirtualFileIndex(),
            new AssetCatalog(),
            new AssetImporterRegistry(),
            runtime.Background,
            runtime.MainThread,
            runtime);
        return new AssetManager(pipeline, runtime.MainThread, runtime);
    }

    [Fact]
    public void ConstructingRuntimeModules_DoesNotMutateGlobalServices()
    {
        Services.Shutdown();

        using var runtime = new ThreadRuntime();
        using var scene = new SceneManager();
        using var assets = CreateAssetManager(runtime);
        using var render = new RenderSystem(new HeadlessRenderBackend(), runtime);

        Assert.Throws<InvalidOperationException>(() => Services.Get<SceneManager>());
        Assert.Throws<InvalidOperationException>(() => Services.Get<AssetManager>());
        Assert.Throws<InvalidOperationException>(() => Services.Get<RenderSystem>());
    }

    [Fact]
    public void RendererResolve_DoesNotUseServicesLookup()
    {
        var source = File.ReadAllText(FindSource("RendererBase.cs"));

        Assert.DoesNotContain("Services.TryGet", source);
        Assert.DoesNotContain("Services.Get", source);
    }

    private static string FindSource(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SilkEngine.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Directory.EnumerateFiles(
                Path.Combine(dir!.FullName, "src"),
                fileName,
                SearchOption.AllDirectories)
            .First();
    }
}
