using SilkEngine.Core.Assets;

namespace SilkEngine.Tests.Core.Assets;

[Collection("Assets")]
public class UnloadTimingTests
{
    private static AssetEntry RegisterManaged(IAsset asset)
    {
        var entry = AssetManager.Cache.GetOrAdd(Guid.NewGuid());
        entry.Data = asset;
        entry.State = AssetState.Ready;
        return entry;
    }

    [Fact]
    public void RefCountZero_AtFrameEnd_MarksUnloaded()
    {
        var tex = new Texture2D { Name = "T" };
        var entry = RegisterManaged(tex);
        AssetManager.TryAddRef(tex);
        AssetManager.TryRelease(tex);
        Assert.Equal(AssetState.Ready, entry.State); // 帧中仍是 Ready

        AssetManager.ProcessCompleted();             // 帧末复核
        Assert.Equal(AssetState.Unloaded, entry.State);
    }

    [Fact]
    public void RefCountNotZero_AtFrameEnd_StaysReady()
    {
        var tex = new Texture2D { Name = "T" };
        var entry = RegisterManaged(tex);
        AssetManager.TryAddRef(tex);

        AssetManager.ProcessCompleted();
        Assert.Equal(AssetState.Ready, entry.State);
    }

    [Fact]
    public void NeverReferenced_Asset_StaysReady()
    {
        // Load 后从未被引用的资产（RefCount 天然 0）不应被帧末误卸载
        var tex = new Texture2D { Name = "T" };
        var entry = RegisterManaged(tex);

        AssetManager.ProcessCompleted();
        Assert.Equal(AssetState.Ready, entry.State);
        Assert.NotNull(entry.Data);
    }

    [Fact]
    public void Reacquired_BeforeFrameEnd_CancelsUnload()
    {
        var tex = new Texture2D { Name = "T" };
        var entry = RegisterManaged(tex);
        AssetManager.TryAddRef(tex);
        AssetManager.TryRelease(tex); // 归零 → 入候选
        AssetManager.TryAddRef(tex);  // 同帧重新引用

        AssetManager.ProcessCompleted();
        Assert.Equal(AssetState.Ready, entry.State);
        Assert.Equal(1, entry.RefCount);
    }

    [Fact]
    public void ProcessUnloadQueue_ClearsCpuData_AndSkipsReady()
    {
        var tex = new Texture2D { Name = "T" };
        var entry = RegisterManaged(tex);
        AssetManager.TryAddRef(tex);
        AssetManager.TryRelease(tex);
        AssetManager.ProcessCompleted();
        Assert.Equal(AssetState.Unloaded, entry.State);
        Assert.NotNull(entry.Data); // 渲染线程帧首处理前 CPU 数据仍在

        AssetManager.ProcessUnloadQueue();
        Assert.Null(entry.Data);    // CPU 侧清引用（GC 回收）
        Assert.Equal(AssetState.Unloaded, entry.State);
    }

    [Fact]
    public void ProcessUnloadQueue_ReadyEntry_IsSkipped()
    {
        var tex = new Texture2D { Name = "T" };
        var entry = RegisterManaged(tex);
        AssetManager.TryAddRef(tex);

        AssetManager.ProcessUnloadQueue(); // 队列空，无操作
        Assert.Equal(AssetState.Ready, entry.State);
        Assert.NotNull(entry.Data);
    }

    [Fact]
    public void Reacquired_AfterUnloadMark_BeforeRelease_CancelsRelease()
    {
        var tex = new Texture2D { Name = "T" };
        var entry = RegisterManaged(tex);
        AssetManager.TryAddRef(tex);
        AssetManager.TryRelease(tex);      // 归零 → 入候选
        AssetManager.ProcessCompleted();   // 帧末 → Unloaded + 入释放队列
        Assert.Equal(AssetState.Unloaded, entry.State);

        AssetManager.TryAddRef(tex);       // 下一帧主线程重新引用（模拟 Tick 中）
        var released = new List<Texture2D>();
        AssetManager.ProcessUnloadQueue(t => released.Add(t));  // 渲染线程帧首

        Assert.Empty(released);            // GL 未释放
        Assert.Equal(AssetState.Ready, entry.State);   // 条目复活
        Assert.NotNull(entry.Data);
        Assert.Equal(1, entry.RefCount);
    }

    [Fact]
    public void Unloaded_Reload_ReturnsFreshAsset()
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"se-unload-{Guid.NewGuid():N}.png");
        System.IO.File.WriteAllBytes(path, PngFixtures.RedPng);
        try
        {
            var first = AssetManager.Load<Texture2D>(path);
            AssetManager.TryAddRef(first);
            AssetManager.TryRelease(first);
            AssetManager.ProcessCompleted();
            AssetManager.ProcessUnloadQueue();

            var again = AssetManager.Load<Texture2D>(path);
            Assert.NotNull(again);
            Assert.NotSame(first, again); // 重新导入，新实例
            Assert.Equal(AssetState.Ready, AssetManager.Cache.Find(AssetManager.PathToGuid(path))!.State);
        }
        finally
        {
            System.IO.File.Delete(path);
        }
    }
}
