using SilkEngine.Assets;
using SilkEngine.Assets.Importer;
using SilkEngine.Assets.VirtualFileSystem;
using SilkEngine.Core;
using SilkEngine.Tests.Core;

namespace SilkEngine.Tests.Core.Assets;

/// <summary>
/// 资产管线加载提交流程测试：AssetId+修订键、失效重载、过期结果丢弃、去重合并、失败重试、LazyAsync。
/// 后台任务经 DeferredTaskScheduler 手动完成，不依赖 sleep。
/// </summary>
[Collection("Assets")]
public class AssetManagerPipelineTests : IDisposable
{
    public void Dispose() => Services.Unregister<AssetManager>();

    /// <summary>测试辅助：内建默认 .png/.jpg 注册的导入器注册表</summary>
    private static AssetImporterRegistry CreateRegistry() => new();

    [Fact]
    public async Task LoadAsync_DeduplicatesAndRejectsStaleCompletion()
    {
        var files = new ControlledAssetFileSystem();
        files.Add("Textures/a.png", [1]);
        var scheduler = new RecordingScheduler();
        using var assets = new AssetManager(files, CreateRegistry(), scheduler);

        var first = assets.LoadAsync<Texture2D>("Textures/a.png");
        files.Replace("Textures/a.png", [2]);
        assets.Invalidate("Textures/a.png");
        var second = assets.LoadAsync<Texture2D>("Textures/a.png");

        Assert.Equal(1, scheduler.ScheduleCalls);
        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task LoadAsync_SameSourceAndType_JoinSameEntryWithStableAssetId()
    {
        var files = new ControlledAssetFileSystem();
        files.Add("Textures/a.png", [1]);
        using var assets = new AssetManager(files, CreateRegistry(), new RecordingScheduler());

        var first = assets.LoadAsync<Texture2D>("Textures/a.png");
        var second = assets.LoadAsync<Texture2D>("Textures/a.png");

        var entry = Assert.Single(assets.Cache.All());
        Assert.NotSame(first, second);
        Assert.False(second.IsDone);
        Assert.Single(entry.Awaiters);
    }

    [Fact]
    public async Task ProcessCompleted_DropsStaleResult_AndReschedulesCurrentRevision()
    {
        var files = new ControlledAssetFileSystem();
        files.Add("Textures/a.png", PngFixtures.RedPng);
        var scheduler = new DeferredTaskScheduler();
        using var assets = new AssetManager(files, CreateRegistry(), scheduler);

        var first = assets.LoadAsync<Texture2D>("Textures/a.png");
        Assert.Equal(1, scheduler.SubmissionCount);
        var entry = Assert.Single(assets.Cache.All());

        files.Replace("Textures/a.png", PngFixtures.RedPng);
        assets.Invalidate("Textures/a.png");
        var second = assets.LoadAsync<Texture2D>("Textures/a.png");
        Assert.Equal(1, scheduler.SubmissionCount);
        Assert.Same(entry, Assert.Single(assets.Cache.All()));

        scheduler.RunNext();       // 旧修订任务完成
        assets.ProcessCompleted(); // 帧末：过期结果丢弃 + 按当前修订重新调度
        Assert.Equal(2, scheduler.SubmissionCount);
        Assert.Equal(AssetState.Loading, entry.State);
        Assert.Null(entry.Data);
        Assert.False(first.IsDone);
        Assert.False(second.IsDone);

        scheduler.RunNext();       // 新修订任务完成
        assets.ProcessCompleted(); // 帧末：提交
        Assert.Equal(AssetState.Ready, entry.State);
        Assert.True(first.IsDone);
        Assert.True(second.IsDone);
        Assert.Null(first.Error);
        Assert.Same(first.Asset, second.Asset);
        Assert.Same(first.Asset, assets.TryResolve<Texture2D>(entry.AssetId));
    }

    [Fact]
    public async Task LoadAsync_CacheHit_OnlyAcceptsCurrentRevision()
    {
        var files = new ControlledAssetFileSystem();
        files.Add("a.png", PngFixtures.RedPng);
        var scheduler = new RecordingScheduler();
        using var assets = new AssetManager(files, CreateRegistry(), scheduler);

        var first = assets.LoadAsync<Texture2D>("a.png");
        assets.ProcessCompleted();
        Assert.Equal(1, scheduler.ScheduleCalls);

        var hit = assets.LoadAsync<Texture2D>("a.png"); // 同修订 → 缓存命中
        Assert.Equal(1, scheduler.ScheduleCalls);
        Assert.True(hit.IsDone);
        Assert.Same(first.Asset, hit.Asset);

        assets.Invalidate("a.png");                     // 源变更 → 修订递增 → 旧数据失效
        var stale = assets.LoadAsync<Texture2D>("a.png");
        Assert.Equal(2, scheduler.ScheduleCalls);
        Assert.False(stale.IsDone);

        assets.ProcessCompleted();
        Assert.True(stale.IsDone);
        Assert.Null(stale.Error);
        Assert.NotSame(first.Asset, stale.Asset); // 新修订重新导入的实例
    }

    [Fact]
    public async Task FailedLoad_RetryAfterSourceFixed_Succeeds()
    {
        var files = new ControlledAssetFileSystem();
        files.Add("broken.png", PngFixtures.CorruptPng);
        using var assets = new AssetManager(files, CreateRegistry(), new RecordingScheduler());

        var failed = assets.LoadAsync<Texture2D>("broken.png");
        assets.ProcessCompleted();
        Assert.True(failed.IsDone);
        Assert.NotNull(failed.Error);
        var entry = Assert.Single(assets.Cache.All());
        Assert.Equal(AssetState.Failed, entry.State);

        files.Replace("broken.png", PngFixtures.RedPng);
        var retry = assets.LoadAsync<Texture2D>("broken.png"); // Failed 条目再次加载 = 重试
        Assert.NotSame(failed, retry);
        Assert.False(retry.IsDone);

        assets.ProcessCompleted();
        Assert.Null(retry.Error);
        Assert.NotNull(retry.Asset);
        Assert.Equal(AssetState.Ready, entry.State);
    }

    [Fact]
    public async Task LazyAsync_DefersSubmission_UntilFirstAccess()
    {
        var files = new ControlledAssetFileSystem();
        files.Add("a.png", PngFixtures.RedPng);
        var scheduler = new DeferredTaskScheduler();
        using var assets = new AssetManager(files, CreateRegistry(), scheduler);

        var req = assets.LoadAsync<Texture2D>("a.png", AsyncLoadMode.LazyAsync);
        Assert.Equal(0, scheduler.SubmissionCount);

        _ = req.Asset; // 首次访问触发实际调度
        Assert.Equal(1, scheduler.SubmissionCount);

        scheduler.RunNext();
        assets.ProcessCompleted();
        Assert.True(req.IsDone);
        Assert.NotNull(req.Asset);
    }

    [Fact]
    public void SyncLoad_ResolvesThroughFileSystemAndCatalog()
    {
        var files = new ControlledAssetFileSystem();
        files.Add("Textures/a.png", PngFixtures.RedPng);
        using var assets = new AssetManager(files, CreateRegistry(), new RecordingScheduler());

        var tex = assets.Load<Texture2D>("Textures/a.png");

        Assert.NotNull(tex);
        Assert.Equal("a", tex.Name);
        var entry = Assert.Single(assets.Cache.All());
        Assert.Equal(AssetState.Ready, entry.State);
        Assert.Same(tex, entry.Data);
        Assert.Same(tex, assets.TryResolve<Texture2D>(entry.AssetId));
    }

    [Fact]
    public async Task TryResolve_AfterInvalidate_ReturnsNullUntilReloaded()
    {
        var files = new ControlledAssetFileSystem();
        files.Add("a.png", PngFixtures.RedPng);
        using var assets = new AssetManager(files, CreateRegistry(), new RecordingScheduler());

        var req = assets.LoadAsync<Texture2D>("a.png");
        assets.ProcessCompleted();
        var entry = Assert.Single(assets.Cache.All());
        Assert.NotNull(assets.TryResolve<Texture2D>(entry.AssetId));

        assets.Invalidate("a.png"); // 源变更 → 旧修订数据不再可解析
        Assert.Null(assets.TryResolve<Texture2D>(entry.AssetId));

        var reload = assets.LoadAsync<Texture2D>("a.png");
        assets.ProcessCompleted();
        Assert.NotNull(assets.TryResolve<Texture2D>(entry.AssetId));
    }

    [Fact]
    public void LoadAsync_UnknownExtension_FailsFast()
    {
        var files = new ControlledAssetFileSystem();
        files.Add("a.bin", [1]);
        using var assets = new AssetManager(files, CreateRegistry(), new RecordingScheduler());

        Assert.Throws<NotSupportedException>(() => assets.LoadAsync<Texture2D>("a.bin"));
    }
}

/// <summary>受控文件系统夹具：组合 InMemoryAssetFileSystem（根 "Assets"），Add 写入、Replace 覆盖（版本递增）</summary>
internal sealed class ControlledAssetFileSystem : IAssetFileSystem
{
    private readonly InMemoryAssetFileSystem _inner = new("Assets");

    public void Add(string path, byte[] content) => _inner.Add(path, content);

    /// <summary>替换文件内容（覆盖写入并递增版本）</summary>
    public void Replace(string path, byte[] content) => _inner.Add(path, content);

    public string Normalize(string path) => _inner.Normalize(path);

    public bool Exists(string path) => _inner.Exists(path);

    public ValueTask<ReadOnlyMemory<byte>> ReadAsync(string path) => _inner.ReadAsync(path);

    public ValueTask<FileMetadata> GetMetadataAsync(string path) => _inner.GetMetadataAsync(path);
}

/// <summary>手动完成调度器夹具：捕获提交的任务，RunNext 逐个执行（结果仅入队，不依赖 sleep）</summary>
internal sealed class DeferredTaskScheduler : ITaskScheduler
{
    private readonly List<Func<CancellationToken, ValueTask>> _pending = [];

    /// <summary>累计提交次数（RunNext 执行不减少）</summary>
    public int SubmissionCount { get; private set; }

    public void Submit(Func<CancellationToken, ValueTask> work)
    {
        SubmissionCount++;
        _pending.Add(work);
    }

    /// <summary>执行下一个已捕获任务</summary>
    public void RunNext()
    {
        var work = _pending[0];
        _pending.RemoveAt(0);
        work(CancellationToken.None).GetAwaiter().GetResult();
    }
}
