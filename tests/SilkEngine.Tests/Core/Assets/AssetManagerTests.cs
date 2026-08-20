using SilkEngine.Core;
using SilkEngine.Core.Assets;
using SilkEngine.Core.Assets.Importer;
using SilkEngine.Threading;

namespace SilkEngine.Tests.Core.Assets;

[Collection("Assets")]
public class AssetManagerTests : IDisposable
{
    /// <summary>测试级清理：注销测试内 ctor 自注册的 AssetManager 实例（Unregister 幂等）</summary>
    public void Dispose() => Services.Unregister<AssetManager>();

    private sealed class FakeAsset : IAsset { }

    private sealed class BlockingScheduler : ITaskExecutor, ITaskScheduler
    {
        public string Name => "Blocking";
        public ThreadContext? Context => null;
        public void Stop() { }
        public void Join() { }
        public void Dispose() { }

        void ITaskScheduler.Submit(Func<CancellationToken, ValueTask> work) =>
            Submit(work, WorkPriority.Normal);

        public IJobHandle Submit(
            Func<CancellationToken, ValueTask> work,
            WorkPriority priority = WorkPriority.Normal,
            CancellationToken ct = default)
            => new TaskJobHandle(Task.Run(async () => await work(ct).ConfigureAwait(false), ct));
    }

    [Fact]
    public void Constructor_NullScheduler_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new AssetManager(null!));
    }

    [Fact]
    public void Load_Sync_ReturnsDecodedTexture_AndCaches()
    {
        using var file = PngTestFile.Create();
        var am = new AssetManager(new RecordingScheduler());
        var a = am.Load<Texture2D>(file.FilePath);
        var b = am.Load<Texture2D>(file.FilePath);
        Assert.Same(a, b);
        Assert.Equal(1, a.ImageData.Width);
        Assert.Equal(255, a.ImageData.Pixels[0]);
    }

    [Fact]
    public void Load_Sync_MissingFile_Throws()
    {
        var missing = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"se-missing-{Guid.NewGuid():N}.png");
        var am = new AssetManager(new RecordingScheduler());
        Assert.Throws<System.IO.FileNotFoundException>(() => am.Load<Texture2D>(missing));
    }

    [Fact]
    public void Load_Sync_TypeMismatch_Throws()
    {
        using var file = PngTestFile.Create();
        var am = new AssetManager(new RecordingScheduler());
        Assert.Throws<InvalidOperationException>(() => am.Load<FakeAsset>(file.FilePath));
    }

    [Fact]
    public void Load_Sync_WhileAsyncLoading_Throws()
    {
        using var file = PngTestFile.Create();
        var am = new AssetManager(new BlockingScheduler());
        var req = am.LoadAsync<Texture2D>(file.FilePath);
        Assert.False(req.IsDone);
        Assert.Throws<InvalidOperationException>(() => am.Load<Texture2D>(file.FilePath));
    }

    [Fact]
    public void LoadAsync_WithRecordingScheduler_CompletesOnProcessCompleted()
    {
        using var file = PngTestFile.Create();
        var am = new AssetManager(new RecordingScheduler());
        var req = am.LoadAsync<Texture2D>(file.FilePath);
        Assert.False(req.IsDone);
        am.ProcessCompleted();
        Assert.True(req.IsDone);
        Assert.Null(req.Error);
        Assert.Equal(1, req.Asset!.ImageData.Width);
        Assert.Equal(255, req.Asset.ImageData.Pixels[0]);
        Assert.Equal(1f, req.Progress);
    }

    [Fact]
    public void LoadAsync_CacheHit_ReturnsCompletedRequest()
    {
        using var file = PngTestFile.Create();
        var am = new AssetManager(new RecordingScheduler());
        var synced = am.Load<Texture2D>(file.FilePath);
        var req = am.LoadAsync<Texture2D>(file.FilePath);
        Assert.True(req.IsDone);
        Assert.Same(synced, req.Asset);
    }

    [Fact]
    public void LoadAsync_SameGuid_SchedulesOnce_AndCompletesBoth()
    {
        using var file = PngTestFile.Create();
        var scheduler = new RecordingScheduler();
        var am = new AssetManager(scheduler);
        var r1 = am.LoadAsync<Texture2D>(file.FilePath);
        var r2 = am.LoadAsync<Texture2D>(file.FilePath);
        Assert.Equal(1, scheduler.ScheduleCalls);
        Assert.False(r1.IsDone);
        Assert.False(r2.IsDone);
        am.ProcessCompleted();
        Assert.True(r1.IsDone);
        Assert.True(r2.IsDone);
        Assert.NotNull(r1.Asset);
        Assert.Same(r1.Asset, r2.Asset);
    }

    [Fact]
    public void LoadAsync_RealTaskExecutor_CompletesWithinTimeout()
    {
        using var file = PngTestFile.Create();
        using var pool = new ThreadPoolExecutor();
        var am = new AssetManager(pool);
        var req = am.LoadAsync<Texture2D>(file.FilePath);
        var done = SpinWait.SpinUntil(() =>
        {
            am.ProcessCompleted();
            return req.IsDone;
        }, TimeSpan.FromSeconds(5));
        Assert.True(done, "工作线程加载超时（5 秒）");
        Assert.Null(req.Error);
        Assert.Equal(255, req.Asset!.ImageData.Pixels[0]);
    }

    [Fact]
    public void LoadAsync_MissingFile_FailsWithError_AndMarksFailed()
    {
        var missing = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"se-missing-{Guid.NewGuid():N}.png");
        var am = new AssetManager(new RecordingScheduler());
        var req = am.LoadAsync<Texture2D>(missing);
        am.ProcessCompleted();
        Assert.True(req.IsDone);
        Assert.IsType<System.IO.FileNotFoundException>(req.Error);
        Assert.Null(req.Asset);
        Assert.Throws<System.IO.FileNotFoundException>(() => req.GetResult());
        var entry = am.Cache.Find(AssetManager.PathToGuid(missing));
        Assert.NotNull(entry);
        Assert.Equal(AssetState.Failed, entry!.State);
    }

    [Fact]
    public void LoadAsync_Failed_ThenRetry_WithFixedFile_Completes()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"se-retry-{Guid.NewGuid():N}.png");
        var am = new AssetManager(new RecordingScheduler());
        var failed = am.LoadAsync<Texture2D>(path);
        am.ProcessCompleted();
        Assert.IsType<System.IO.FileNotFoundException>(failed.Error);

        System.IO.File.WriteAllBytes(path, PngFixtures.RedPng);
        try
        {
            var ok = am.LoadAsync<Texture2D>(path);
            am.ProcessCompleted();
            Assert.Null(ok.Error);
            Assert.Equal(1, ok.Asset!.ImageData.Width);
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }

    [Fact]
    public void LoadAsync_LazyAsync_FirstAssetAccess_TriggersLoad()
    {
        using var file = PngTestFile.Create();
        var scheduler = new RecordingScheduler();
        var am = new AssetManager(scheduler);
        var req = am.LoadAsync<Texture2D>(file.FilePath, AsyncLoadMode.LazyAsync);
        Assert.False(req.IsDone);
        Assert.Equal(0, scheduler.ScheduleCalls); // 登记不调度
        Assert.Null(req.Asset);                    // 首次访问 → 触发调度；帧末前仍为 null
        Assert.Equal(1, scheduler.ScheduleCalls);  // 已触发且仅触发一次
        Assert.Null(req.Asset);
        Assert.Equal(1, scheduler.ScheduleCalls);  // 重复访问不重复调度

        am.ProcessCompleted();
        Assert.True(req.IsDone);
        Assert.Equal(1, req.Asset!.ImageData.Width);
    }

    [Fact]
    public void ProcessCompleted_WithNoWork_DoesNotThrow()
    {
        var am = new AssetManager(new RecordingScheduler());
        am.ProcessCompleted();
    }

    [Fact]
    public void LoadAsync_UsesInjectedScheduler()
    {
        var scheduler = new RecordingScheduler();
        var am = new AssetManager(scheduler);
        using var file = PngTestFile.Create();
        var req = am.LoadAsync<Texture2D>(file.FilePath);
        am.ProcessCompleted();
        Assert.True(req.IsDone);
        Assert.Equal(1, scheduler.ScheduleCalls);
    }

    [Fact]
    public async Task AwaitOperator_ResumesOnProcessCompleted()
    {
        using var file = PngTestFile.Create();
        var am = new AssetManager(new RecordingScheduler());
        var req = am.LoadAsync<Texture2D>(file.FilePath);
        Texture2D? awaited = null;
        var task = Task.Run(async () => awaited = await req);
        var done = SpinWait.SpinUntil(() =>
        {
            am.ProcessCompleted();
            return task.IsCompleted;
        }, TimeSpan.FromSeconds(5));
        Assert.True(done, "await 续延未在 5 秒内恢复");
        await task;
        Assert.NotNull(awaited);
        Assert.Equal(255, awaited!.ImageData.Pixels[0]);
    }

    private class TestWriter : ILogWriter
    {
        public List<string> Messages = new();
        public void Write(string msg) => Messages.Add(msg);
    }

    [Fact]
    public void LoadAsync_LogSwitchOn_EmitsStart()
    {
        using var file = PngTestFile.Create();
        var tw = new TestWriter();
        var minLevel = Log.MinLevel;
        Log.MinLevel = LogLevel.Debug;
        Log.AddWriter(tw);
        try
        {
            LogConfig.Assets = true;
            var am = new AssetManager(new RecordingScheduler());
            am.LoadAsync<Texture2D>(file.FilePath);
            Assert.Contains(tw.Messages, m => m.Contains("[Assets]") && m.Contains("Load"));
        }
        finally
        {
            Log.RemoveWriter(tw);
            LogConfig.Assets = true;
            Log.MinLevel = minLevel;
        }
    }
}
