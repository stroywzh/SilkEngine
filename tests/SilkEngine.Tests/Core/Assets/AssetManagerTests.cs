using SilkEngine.Core.Assets;
using SilkEngine.Core.Assets.Importer;
using SilkEngine.Threading;

namespace SilkEngine.Tests.Core.Assets;

[Collection("Assets")]
public class AssetManagerTests
{
    private sealed class FakeAsset : IAsset { }

    private sealed class BlockingScheduler : IWorkerScheduler
    {
        public void Schedule(
            Func<Task> work,
            WorkPriority priority = WorkPriority.Normal,
            CancellationToken ct = default
        ) { }
    }

    [Fact]
    public void Load_Sync_ReturnsDecodedTexture_AndCaches()
    {
        using var file = PngTestFile.Create();
        var a = AssetManager.Load<Texture2D>(file.FilePath);
        var b = AssetManager.Load<Texture2D>(file.FilePath);
        Assert.Same(a, b);
        Assert.Equal(1, a.ImageData.Width);
        Assert.Equal(255, a.ImageData.Pixels[0]);
    }

    [Fact]
    public void Load_Sync_MissingFile_Throws()
    {
        var missing = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"se-missing-{Guid.NewGuid():N}.png");
        Assert.Throws<System.IO.FileNotFoundException>(() => AssetManager.Load<Texture2D>(missing));
    }

    [Fact]
    public void Load_Sync_TypeMismatch_Throws()
    {
        using var file = PngTestFile.Create();
        Assert.Throws<InvalidOperationException>(() => AssetManager.Load<FakeAsset>(file.FilePath));
    }

    [Fact]
    public void Load_Sync_WhileAsyncLoading_Throws()
    {
        using var file = PngTestFile.Create();
        AssetManager.SetSchedulerForTests(new BlockingScheduler());
        try
        {
            var req = AssetManager.LoadAsync<Texture2D>(file.FilePath);
            Assert.False(req.IsDone);
            Assert.Throws<InvalidOperationException>(() => AssetManager.Load<Texture2D>(file.FilePath));
        }
        finally
        {
            AssetManager.SetSchedulerForTests(null);
        }
    }

    [Fact]
    public void LoadAsync_WithRecordingScheduler_CompletesOnProcessCompleted()
    {
        using var file = PngTestFile.Create();
        AssetManager.SetSchedulerForTests(new RecordingScheduler());
        try
        {
            var req = AssetManager.LoadAsync<Texture2D>(file.FilePath);
            Assert.False(req.IsDone);
            AssetManager.ProcessCompleted();
            Assert.True(req.IsDone);
            Assert.Null(req.Error);
            Assert.Equal(1, req.Asset!.ImageData.Width);
            Assert.Equal(255, req.Asset.ImageData.Pixels[0]);
            Assert.Equal(1f, req.Progress);
        }
        finally
        {
            AssetManager.SetSchedulerForTests(null);
        }
    }

    [Fact]
    public void LoadAsync_CacheHit_ReturnsCompletedRequest()
    {
        using var file = PngTestFile.Create();
        AssetManager.SetSchedulerForTests(new RecordingScheduler());
        try
        {
            var synced = AssetManager.Load<Texture2D>(file.FilePath);
            var req = AssetManager.LoadAsync<Texture2D>(file.FilePath);
            Assert.True(req.IsDone);
            Assert.Same(synced, req.Asset);
        }
        finally
        {
            AssetManager.SetSchedulerForTests(null);
        }
    }

    [Fact]
    public void LoadAsync_SameGuid_SchedulesOnce_AndCompletesBoth()
    {
        using var file = PngTestFile.Create();
        var scheduler = new RecordingScheduler();
        AssetManager.SetSchedulerForTests(scheduler);
        try
        {
            var r1 = AssetManager.LoadAsync<Texture2D>(file.FilePath);
            var r2 = AssetManager.LoadAsync<Texture2D>(file.FilePath);
            Assert.Equal(1, scheduler.ScheduleCalls);
            Assert.False(r1.IsDone);
            Assert.False(r2.IsDone);
            AssetManager.ProcessCompleted();
            Assert.True(r1.IsDone);
            Assert.True(r2.IsDone);
            Assert.NotNull(r1.Asset);
            Assert.Same(r1.Asset, r2.Asset);
        }
        finally
        {
            AssetManager.SetSchedulerForTests(null);
        }
    }

    [Fact]
    public void LoadAsync_RealWorkerThread_CompletesWithinTimeout()
    {
        using var file = PngTestFile.Create();
        var pool = new EngineThreadPool(2);
        AssetManager.SetSchedulerForTests(pool);
        try
        {
            var req = AssetManager.LoadAsync<Texture2D>(file.FilePath);
            var done = SpinWait.SpinUntil(() =>
            {
                AssetManager.ProcessCompleted();
                return req.IsDone;
            }, TimeSpan.FromSeconds(5));
            Assert.True(done, "工作线程加载超时（5 秒）");
            Assert.Null(req.Error);
            Assert.Equal(255, req.Asset!.ImageData.Pixels[0]);
        }
        finally
        {
            pool.Dispose();
            AssetManager.SetSchedulerForTests(null);
        }
    }

    [Fact]
    public void LoadAsync_MissingFile_FailsWithError_AndMarksFailed()
    {
        var missing = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"se-missing-{Guid.NewGuid():N}.png");
        AssetManager.SetSchedulerForTests(new RecordingScheduler());
        try
        {
            var req = AssetManager.LoadAsync<Texture2D>(missing);
            AssetManager.ProcessCompleted();
            Assert.True(req.IsDone);
            Assert.IsType<System.IO.FileNotFoundException>(req.Error);
            Assert.Null(req.Asset);
            Assert.Throws<System.IO.FileNotFoundException>(() => req.GetResult());
            var entry = AssetManager.Cache.Find(AssetManager.PathToGuid(missing));
            Assert.NotNull(entry);
            Assert.Equal(AssetState.Failed, entry!.State);
        }
        finally
        {
            AssetManager.SetSchedulerForTests(null);
        }
    }

    [Fact]
    public void LoadAsync_Failed_ThenRetry_WithFixedFile_Completes()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"se-retry-{Guid.NewGuid():N}.png");
        AssetManager.SetSchedulerForTests(new RecordingScheduler());
        try
        {
            var failed = AssetManager.LoadAsync<Texture2D>(path);
            AssetManager.ProcessCompleted();
            Assert.IsType<System.IO.FileNotFoundException>(failed.Error);

            System.IO.File.WriteAllBytes(path, PngFixtures.RedPng);
            try
            {
                var ok = AssetManager.LoadAsync<Texture2D>(path);
                AssetManager.ProcessCompleted();
                Assert.Null(ok.Error);
                Assert.Equal(1, ok.Asset!.ImageData.Width);
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }
        finally
        {
            AssetManager.SetSchedulerForTests(null);
        }
    }

    [Fact]
    public void LoadAsync_LazyAsync_ThrowsNotSupported()
    {
        using var file = PngTestFile.Create();
        Assert.Throws<NotSupportedException>(() =>
            AssetManager.LoadAsync<Texture2D>(file.FilePath, AsyncLoadMode.LazyAsync));
    }

    [Fact]
    public void ProcessCompleted_WithNoWork_DoesNotThrow()
    {
        AssetManager.ProcessCompleted();
    }

    [Fact]
    public async Task AwaitOperator_ResumesOnProcessCompleted()
    {
        using var file = PngTestFile.Create();
        AssetManager.SetSchedulerForTests(new RecordingScheduler());
        try
        {
            var req = AssetManager.LoadAsync<Texture2D>(file.FilePath);
            Texture2D? awaited = null;
            var task = Task.Run(async () => awaited = await req);
            var done = SpinWait.SpinUntil(() =>
            {
                AssetManager.ProcessCompleted();
                return task.IsCompleted;
            }, TimeSpan.FromSeconds(5));
            Assert.True(done, "await 续延未在 5 秒内恢复");
            await task;
            Assert.NotNull(awaited);
            Assert.Equal(255, awaited!.ImageData.Pixels[0]);
        }
        finally
        {
            AssetManager.SetSchedulerForTests(null);
        }
    }
}
