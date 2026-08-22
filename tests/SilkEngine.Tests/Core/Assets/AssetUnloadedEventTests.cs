using SilkEngine.Core;
using SilkEngine.Assets;

namespace SilkEngine.Tests.Core.Assets;

// 与卸载时序测试同集合：事件在帧末发布（主线程），消费在渲染线程帧首——本类只验证发布侧
[Collection("Assets")]
public class AssetUnloadedEventTests : IDisposable
{
    /// <summary>测试级清理：注销测试内 ctor 自注册的 AssetManager 实例（Unregister 幂等）</summary>
    public void Dispose() => Services.Unregister<AssetManager>();

    [Fact]
    public void AssetUnloaded_Fired_WhenRefCountReachesZero_AtFrameEnd()
    {
        var am = new AssetManager(new RecordingScheduler());
        var received = new List<IAsset>();
        am.AssetUnloaded += a => received.Add(a);
        using var file = PngTestFile.Create();
        var tex = am.Load<Texture2D>(file.FilePath);

        am.TryAddRef(tex);
        am.TryRelease(tex);          // 帧中归零 → 入候选
        am.ProcessCompleted();       // 帧末复核 → Unloaded + 事件

        Assert.Single(received);
        Assert.Same(tex, received[0]);
    }

    [Fact]
    public void AssetUnloaded_NotFired_WhenStillReferenced()
    {
        var am = new AssetManager(new RecordingScheduler());
        var received = new List<IAsset>();
        am.AssetUnloaded += a => received.Add(a);
        using var file = PngTestFile.Create();
        var tex = am.Load<Texture2D>(file.FilePath);

        am.TryAddRef(tex);
        am.ProcessCompleted();

        Assert.Empty(received);
        Assert.Equal(AssetState.Ready, am.Cache.Find(AssetManager.PathToGuid(file.FilePath))!.State);
    }

    [Fact]
    public void AssetUnloaded_NotFired_WhenReacquiredBeforeFrameEnd()
    {
        var am = new AssetManager(new RecordingScheduler());
        var received = new List<IAsset>();
        am.AssetUnloaded += a => received.Add(a);
        using var file = PngTestFile.Create();
        var tex = am.Load<Texture2D>(file.FilePath);

        am.TryAddRef(tex);
        am.TryRelease(tex);
        am.TryAddRef(tex);           // 同帧重新引用 → 卸载取消
        am.ProcessCompleted();

        Assert.Empty(received);
        Assert.Equal(AssetState.Ready, am.Cache.Find(AssetManager.PathToGuid(file.FilePath))!.State);
    }
}
