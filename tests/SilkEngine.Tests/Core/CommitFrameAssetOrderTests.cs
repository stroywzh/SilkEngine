using SilkEngine.Core;
using SilkEngine.Assets;
using SilkEngine.Assets.Importer;
using SilkEngine.Assets.VirtualFileSystem;
using SilkEngine.Scene;
using SilkEngine.Threading;
using Object = SilkEngine.Core.Object;

namespace SilkEngine.Tests.Core;

using Scene = SilkEngine.Scene.Scene;

[Collection("Assets")]
public class CommitFrameAssetOrderTests : IDisposable
{
    private Fixture? _fx;

    /// <summary>测试级清理：注销测试内 ctor 自注册的 AssetManager 实例（Unregister 幂等）</summary>
    public void Dispose() => Services.Unregister<AssetManager>();

    private sealed class Fixture : IDisposable
    {
        public ComponentRegistry Reg = new();
        public FrameSnapshotManager Mgr = new();
        public List<SceneManager.DestroyEntry> DestroyQueue = new();
        public AssetManager Am =
            new(new InMemoryAssetFileSystem("Assets"), new AssetImporterRegistry(), new RecordingScheduler());

        public Fixture()
        {
            Object.DestroyHandler += OnDestroy;
        }

        // 独立销毁队列：不构造 SceneManager（其 ctor 自注册进 Services，与 "SceneManager" 集合并行测试竞争）
        private void OnDestroy(Object obj, float delay) =>
            DestroyQueue.Add(new SceneManager.DestroyEntry { Target = obj, Delay = delay });

        public void Dispose() => Object.DestroyHandler -= OnDestroy;
    }

    private class ReleaseOnDestroy : MonoBehaviour
    {
        public Texture2D? Target;
        public AssetManager? Manager;
        public override void OnDestroy() => Manager!.TryRelease(Target!);
    }

    [Fact]
    public void CommitFrame_AssetReleasedInOnDestroy_MigratedByProcessCompletedAfter()
    {
        using var fx = new Fixture();
        _fx = fx;
        using var runtime = new ThreadRuntime();
        runtime.RegisterMainThread();
        var scene = new Scene("T");
        var go = new GameObject();
        var releaser = go.AddComponent<ReleaseOnDestroy>(fx.Reg);
        var tex = new Texture2D();
        var entry = fx.Am.Cache.GetOrAdd(new AssetId(Guid.NewGuid()));
        entry.Data = tex;
        entry.State = AssetState.Ready;
        fx.Am.TryAddRef(tex);              // RefCount 0 → 1
        releaser.Target = tex;
        releaser.Manager = fx.Am;
        scene.AddRootObject(go);
        fx.Reg.ApplyPending();
        fx.Mgr.CommitPending(fx.Reg, fx.DestroyQueue, scene, 0f);

        Object.Destroy(go);
        fx.Mgr.CommitPending(fx.Reg, fx.DestroyQueue, scene, 0f); // 销毁处理 → OnDestroy 释放 → 归零候选
        Assert.Equal(AssetState.Ready, entry.State);                  // 尚未迁移（ProcessCompleted 未跑）

        CommitFrameForTests(runtime, () => fx.Am.ProcessCompleted()); // 完整帧提交：快照 swap → 排空 FrameCommit → 资产应用
        Assert.Equal(AssetState.Unloaded, entry.State);               // 同帧迁移：顺序契约
    }

    [Fact]
    public void FrameCommit_DrainsDispatcherAfterSnapshotSwapBeforeAssetApply()
    {
        using var fx = new Fixture();
        _fx = fx;
        using var runtime = new ThreadRuntime();
        runtime.RegisterMainThread();
        var order = new List<string>();
        runtime.MainThread.Post(MainThreadPhase.FrameCommit, () => order.Add("dispatch"));

        CommitFrameForTests(runtime, () => order.Add("asset-apply"));

        Assert.Equal(["dispatch", "asset-apply"], order);
    }

    /// <summary>帧提交契约镜像（与 FrameCommitter.Commit 顺序一致：快照 swap → 排空 FrameCommit → 资产应用）。</summary>
    private void CommitFrameForTests(ThreadRuntime runtime, Action assetApply)
    {
        var fx = _fx!;
        fx.Mgr.CommitPending(fx.Reg, fx.DestroyQueue, null, Time.DeltaTime);
        runtime.Drain(MainThreadPhase.FrameCommit);
        assetApply();
    }
}
