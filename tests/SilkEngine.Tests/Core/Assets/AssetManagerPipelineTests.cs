using SilkEngine.Assets;
using SilkEngine.Assets.Importer;
using SilkEngine.Assets.VirtualFileSystem;
using SilkEngine.Core;
using SilkEngine.Threading;
using SilkEngine.Tests.Core;

namespace SilkEngine.Tests.Core.Assets;

/// <summary>
/// 资产门面管线测试：AssetManager 降级为 Payload 门面（路径解析 + Pipeline 转发 + 缓存应用），
/// 不持有 Importer/调度器；同键加载返回规范 Payload 实例；失败重试；失效后解析失效。
/// </summary>
[Collection("Assets")]
public class AssetManagerPipelineTests : IDisposable
{
    public void Dispose() => Services.Unregister<AssetManager>();

    [Fact]
    public void AssetManager_DoesNotOwnImporterOrWorkerScheduler()
    {
        var source = File.ReadAllText(FindSource("AssetManager.cs"));

        Assert.DoesNotContain("IAssetImporter", source);
        Assert.DoesNotContain("ReadAsync", source);
        Assert.DoesNotContain("Task.Run", source);
        Assert.Contains("IAssetPipeline", source);
    }

    [Fact]
    public async Task LoadAsync_ReturnsCanonicalPayloadInstance()
    {
        using var fx = new ManagerFixture();
        var first = await fx.Manager.LoadAsync<TextureAsset>("Textures/a.png");
        var second = await fx.Manager.LoadAsync<TextureAsset>("Textures/a.png");

        Assert.Same(first, second);
        Assert.Equal(1, fx.Pipeline.ExecutionCount);
    }

    [Fact]
    public async Task LoadAsync_UnknownExtension_FailsFast()
    {
        using var fx = new ManagerFixture();

        Assert.Throws<NotSupportedException>(() => fx.Manager.LoadAsync<TextureAsset>("a.bin"));
    }    [Fact]
    public void SyncLoad_ResolvesThroughPipelineAndAppliesAtFrameCommit()
    {
        using var fx = new ManagerFixture();

        var tex = fx.Manager.Load<TextureAsset>("Textures/a.png");

        Assert.NotNull(tex);
        Assert.Equal("a", tex.Name);
        fx.Runtime.Drain(MainThreadPhase.FrameCommit);
        var entry = Assert.Single(fx.Manager.Cache.All());
        Assert.Equal(AssetState.Ready, entry.State);
        Assert.Same(tex, entry.Payload);
        Assert.Same(tex, fx.Manager.TryResolve<TextureAsset>(entry.AssetId));
    }

    [Fact]
    public async Task FailedLoad_RetryAfterSourceFixed_Succeeds()
    {
        using var fx = new ManagerFixture();

        Assert.Throws<InvalidOperationException>(() => fx.Manager.Load<TextureAsset>("broken.png"));

        fx.Files.Add("broken.png", PngFixtures.RedPng);
        fx.Manager.Invalidate("broken.png");
        var retry = fx.Manager.Load<TextureAsset>("broken.png");

        Assert.NotNull(retry);
        Assert.Equal("broken", retry.Name);
    }

    [Fact]
    public async Task LoadAsync_Failure_PropagatesToOperation()
    {
        using var fx = new ManagerFixture();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fx.Manager.LoadAsync<TextureAsset>("broken.png").AsTask());
    }

    [Fact]
    public async Task TryResolve_AfterInvalidate_ReturnsNullUntilReloaded()
    {
        using var fx = new ManagerFixture();
        var first = fx.Manager.Load<TextureAsset>("Textures/a.png");
        fx.Runtime.Drain(MainThreadPhase.FrameCommit);
        var entry = Assert.Single(fx.Manager.Cache.All());
        Assert.NotNull(fx.Manager.TryResolve<TextureAsset>(entry.AssetId));

        fx.Manager.Invalidate("Textures/a.png");
        Assert.Null(fx.Manager.TryResolve<TextureAsset>(entry.AssetId));

        var reload = fx.Manager.Load<TextureAsset>("Textures/a.png");
        fx.Runtime.Drain(MainThreadPhase.FrameCommit);
        Assert.NotNull(fx.Manager.TryResolve<TextureAsset>(entry.AssetId));
        Assert.NotSame(first, reload);
    }

    private static string FindSource(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "SilkEngine", "Assets", fileName);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        throw new InvalidOperationException($"Source file '{fileName}' not found.");
    }

    /// <summary>门面测试夹具：内存文件系统 + 同步调度管线 + 已索引路径（测试夹具）</summary>
    private sealed class ManagerFixture : IDisposable
    {
        public ThreadRuntime Runtime { get; } = new();

        public InMemoryAssetFileSystem Files { get; } = new("Assets");

        public AssetPipeline Pipeline { get; }

        public AssetManager Manager { get; }

        public ManagerFixture()
        {
            Runtime.RegisterMainThread();
            Files.Add("Textures/a.png", PngFixtures.RedPng);
            Files.Add("broken.png", PngFixtures.CorruptPng);
            Files.Add("a.bin", [1]);
            var index = new InMemoryVirtualFileIndex();
            index.Apply(ScanResult.FromFiles([
                ScanFile.File("Textures/a.png", 1),
                ScanFile.File("broken.png", 1),
                ScanFile.File("a.bin", 1),
            ]));
            Pipeline = new AssetPipeline(
                Files,
                index,
                new AssetCatalog(),
                new AssetImporterRegistry(),
                new SyncBackgroundScheduler(),
                Runtime.MainThread,
                Runtime);
            Manager = new AssetManager(Pipeline, Runtime.MainThread, Runtime);
        }

        public void Dispose()
        {
            Services.Unregister<AssetManager>();
            Runtime.Dispose();
        }
    }
}
