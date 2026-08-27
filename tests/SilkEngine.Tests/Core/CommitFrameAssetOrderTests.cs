using SilkEngine.Core;
using SilkEngine.Assets;
using SilkEngine.Assets.Importer;
using SilkEngine.Assets.VirtualFileSystem;
using SilkEngine.Scene;
using SilkEngine.Threading;
using SilkEngine.Tests.Core.Assets;

using Object = SilkEngine.Core.Object;

namespace SilkEngine.Tests.Core;

using Scene = SilkEngine.Scene.Scene;

/// <summary>
/// 帧提交顺序契约测试：快照 swap → 排空 FrameCommit（管线结果随 FrameCommit 应用缓存）。
/// </summary>
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
        public ThreadRuntime Runtime = new();
        public AssetManager Am;

        public Fixture()
        {
            Object.DestroyHandler += OnDestroy;
            Runtime.RegisterMainThread();
            var files = new InMemoryAssetFileSystem("Assets");
            files.Add("a.png", PngFixtures.RedPng);
            var index = new InMemoryVirtualFileIndex();
            index.Apply(ScanResult.FromFiles([ScanFile.File("a.png", 1)]));
            var pipeline = new AssetPipeline(
                files,
                index,
                new AssetCatalog(),
                new AssetImporterRegistry(),
                new SyncBackgroundScheduler(),
                Runtime.MainThread,
                Runtime);
            Am = new AssetManager(pipeline, Runtime.MainThread, Runtime);
        }

        // 独立销毁队列：不构造 SceneManager（其 ctor 自注册进 Services，与 "SceneManager" 集合并行测试竞争）
        private void OnDestroy(Object obj, float delay) =>
            DestroyQueue.Add(new SceneManager.DestroyEntry { Target = obj, Delay = delay });

        public void Dispose()
        {
            Object.DestroyHandler -= OnDestroy;
            Runtime.Dispose();
        }
    }

    private class ReleaseOnDestroy : MonoBehaviour
    {
        public Texture2D? Target;
        public AssetManager? Manager;
        public override void OnDestroy() => Manager!.TryRelease(Target!);
    }

    [Fact]
    public void CommitFrame_AssetReleasedInOnDestroy_ThenPipelineResultApplied()
    {
        using var fx = new Fixture();
        _fx = fx;
        var scene = new Scene("T");
        var go = new GameObject();
        var releaser = go.AddComponent<ReleaseOnDestroy>(fx.Reg);
        var tex = new Texture2D();
        var entry = fx.Am.Cache.GetOrAdd(new AssetId(Guid.NewGuid()));
        entry.Payload = tex;
        entry.State = AssetState.Ready;
        fx.Am.TryAddRef(tex);              // RefCount 0 → 1
        releaser.Target = tex;
        releaser.Manager = fx.Am;
        scene.AddRootObject(go);
        fx.Reg.ApplyPending();
        fx.Mgr.CommitPending(fx.Reg, fx.DestroyQueue, scene, 0f);

        Object.Destroy(go);
        var payload = fx.Am.LoadAsync<TextureAsset>("a.png").AsTask().GetAwaiter().GetResult();
        Assert.DoesNotContain(fx.Am.Cache.All(), e => ReferenceEquals(e.Payload, payload));

        CommitFrameForTests(fx.Runtime);   // 完整帧提交：快照 swap → 排空 FrameCommit → 资产结果应用

        Assert.Equal(0, entry.RefCount);                                             // OnDestroy 释放已执行
        Assert.Contains(fx.Am.Cache.All(), e => ReferenceEquals(e.Payload, payload)); // 资产结果随帧提交应用
    }

    [Fact]
    public void FrameCommit_DrainsDispatcherAfterSnapshotSwapBeforeAssetApply()
    {
        using var fx = new Fixture();
        _fx = fx;
        var order = new List<string>();
        fx.Runtime.MainThread.Post(MainThreadPhase.FrameCommit, () => order.Add("dispatch"));
        var payload = fx.Am.LoadAsync<TextureAsset>("a.png").AsTask().GetAwaiter().GetResult();

        CommitFrameForTests(fx.Runtime);

        Assert.Equal(["dispatch"], order);
        Assert.Contains(fx.Am.Cache.All(), e => ReferenceEquals(e.Payload, payload));
    }

    /// <summary>帧提交契约镜像（与 FrameCommitter.Commit 顺序一致：快照 swap → 排空 FrameCommit）。</summary>
    private void CommitFrameForTests(ThreadRuntime runtime)
    {
        var fx = _fx!;
        fx.Mgr.CommitPending(fx.Reg, fx.DestroyQueue, null, Time.DeltaTime);
        runtime.Drain(MainThreadPhase.FrameCommit);
    }
}
