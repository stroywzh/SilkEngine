using SilkEngine.Core;
using SilkEngine.Core.Assets;

namespace SilkEngine.Tests.Core.Assets;

[Collection("Assets")]
public class UnloadTimingTests : IDisposable
{
    /// <summary>测试级清理：注销测试内 ctor 自注册的 AssetManager 实例（Unregister 幂等）</summary>
    public void Dispose() => Services.Unregister<AssetManager>();

    private static AssetEntry RegisterManaged(AssetManager am, IAsset asset)
    {
        var entry = am.Cache.GetOrAdd(Guid.NewGuid());
        entry.Data = asset;
        entry.State = AssetState.Ready;
        return entry;
    }

    [Fact]
    public void RefCountZero_AtFrameEnd_MarksUnloaded()
    {
        var am = new AssetManager(new RecordingScheduler());
        var tex = new Texture2D { Name = "T" };
        var entry = RegisterManaged(am, tex);
        am.TryAddRef(tex);
        am.TryRelease(tex);
        Assert.Equal(AssetState.Ready, entry.State); // 帧中仍是 Ready

        am.ProcessCompleted();             // 帧末复核
        Assert.Equal(AssetState.Unloaded, entry.State);
    }

    [Fact]
    public void RefCountNotZero_AtFrameEnd_StaysReady()
    {
        var am = new AssetManager(new RecordingScheduler());
        var tex = new Texture2D { Name = "T" };
        var entry = RegisterManaged(am, tex);
        am.TryAddRef(tex);

        am.ProcessCompleted();
        Assert.Equal(AssetState.Ready, entry.State);
    }

    [Fact]
    public void NeverReferenced_Asset_StaysReady()
    {
        // Load 后从未被引用的资产（RefCount 天然 0）不应被帧末误卸载
        var am = new AssetManager(new RecordingScheduler());
        var tex = new Texture2D { Name = "T" };
        var entry = RegisterManaged(am, tex);

        am.ProcessCompleted();
        Assert.Equal(AssetState.Ready, entry.State);
        Assert.NotNull(entry.Data);
    }

    [Fact]
    public void Reacquired_BeforeFrameEnd_CancelsUnload()
    {
        var am = new AssetManager(new RecordingScheduler());
        var tex = new Texture2D { Name = "T" };
        var entry = RegisterManaged(am, tex);
        am.TryAddRef(tex);
        am.TryRelease(tex); // 归零 → 入候选
        am.TryAddRef(tex);  // 同帧重新引用

        am.ProcessCompleted();
        Assert.Equal(AssetState.Ready, entry.State);
        Assert.Equal(1, entry.RefCount);
    }

    [Fact]
    public void ProcessUnloadQueue_ClearsCpuData_AndSkipsReady()
    {
        var am = new AssetManager(new RecordingScheduler());
        var tex = new Texture2D { Name = "T" };
        var entry = RegisterManaged(am, tex);
        am.TryAddRef(tex);
        am.TryRelease(tex);
        am.ProcessCompleted();
        Assert.Equal(AssetState.Unloaded, entry.State);
        Assert.NotNull(entry.Data); // 渲染线程帧首处理前 CPU 数据仍在

        am.ProcessUnloadQueue();
        Assert.Null(entry.Data);    // CPU 侧清引用（GC 回收）
        Assert.Equal(AssetState.Unloaded, entry.State);
    }

    [Fact]
    public void ProcessUnloadQueue_ReadyEntry_IsSkipped()
    {
        var am = new AssetManager(new RecordingScheduler());
        var tex = new Texture2D { Name = "T" };
        var entry = RegisterManaged(am, tex);
        am.TryAddRef(tex);

        am.ProcessUnloadQueue(); // 队列空，无操作
        Assert.Equal(AssetState.Ready, entry.State);
        Assert.NotNull(entry.Data);
    }

    [Fact]
    public void Reacquired_AfterUnloadMark_BeforeRelease_CancelsRelease()
    {
        var am = new AssetManager(new RecordingScheduler());
        var tex = new Texture2D { Name = "T" };
        var entry = RegisterManaged(am, tex);
        am.TryAddRef(tex);
        am.TryRelease(tex);      // 归零 → 入候选
        am.ProcessCompleted();   // 帧末 → Unloaded + 入释放队列
        Assert.Equal(AssetState.Unloaded, entry.State);

        am.TryAddRef(tex);       // 下一帧主线程重新引用（模拟 Tick 中）
        var released = new List<Texture2D>();
        am.ProcessUnloadQueue(t => released.Add(t));  // 渲染线程帧首

        Assert.Empty(released);            // GL 未释放
        Assert.Equal(AssetState.Ready, entry.State);   // 条目复活
        Assert.NotNull(entry.Data);
        Assert.Equal(1, entry.RefCount);
    }

    [Fact]
    public void Unloaded_Reload_ReturnsFreshAsset()
    {
        var am = new AssetManager(new RecordingScheduler());
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"se-unload-{Guid.NewGuid():N}.png");
        System.IO.File.WriteAllBytes(path, PngFixtures.RedPng);
        try
        {
            var first = am.Load<Texture2D>(path);
            am.TryAddRef(first);
            am.TryRelease(first);
            am.ProcessCompleted();
            am.ProcessUnloadQueue();

            var again = am.Load<Texture2D>(path);
            Assert.NotNull(again);
            Assert.NotSame(first, again); // 重新导入，新实例
            Assert.Equal(AssetState.Ready, am.Cache.Find(AssetManager.PathToGuid(path))!.State);
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }
}
