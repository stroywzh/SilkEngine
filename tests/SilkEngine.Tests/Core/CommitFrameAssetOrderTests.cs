using SilkEngine.Core;
using SilkEngine.Assets;
using SilkEngine.Scene;
using SilkEngine.Tests.Core.Assets;
using Object = SilkEngine.Core.Object;

namespace SilkEngine.Tests.Core;

using Scene = SilkEngine.Scene.Scene;

[Collection("Assets")]
public class CommitFrameAssetOrderTests : IDisposable
{
    /// <summary>测试级清理：注销测试内 ctor 自注册的 SceneManager/AssetManager 实例（Unregister 幂等）</summary>
    public void Dispose()
    {
        Services.Unregister<SceneManager>();
        Services.Unregister<AssetManager>();
    }

    private sealed class Fixture : IDisposable
    {
        public ComponentRegistry Reg = new();
        public FrameSnapshotManager Mgr = new();
        public SceneManager Sm = new();
        public AssetManager Am = new(new RecordingScheduler());

        public Fixture()
        {
            Services.Unregister<SceneManager>(); // 消除 ctor 自注册窗口（实例自足，无 ambient 依赖）
            Sm.Attach(Reg, Mgr);
            Object.DestroyHandler += OnDestroy;
        }

        private void OnDestroy(Object obj, float delay) =>
            Sm._destroyQueue.Add(new SceneManager.DestroyEntry { Target = obj, Delay = delay });

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
        var scene = new Scene("T");
        var go = new GameObject();
        var releaser = go.AddComponent<ReleaseOnDestroy>(fx.Reg);
        var tex = new Texture2D();
        var entry = fx.Am.Cache.GetOrAdd(Guid.NewGuid());
        entry.Data = tex;
        entry.State = AssetState.Ready;
        fx.Am.TryAddRef(tex);              // RefCount 0 → 1
        releaser.Target = tex;
        releaser.Manager = fx.Am;
        scene.AddRootObject(go);
        fx.Reg.ApplyPending();
        fx.Mgr.CommitPending(fx.Reg, fx.Sm._destroyQueue, scene, 0f);

        Object.Destroy(go);
        fx.Mgr.CommitPending(fx.Reg, fx.Sm._destroyQueue, scene, 0f); // 销毁处理 → OnDestroy 释放 → 归零候选
        Assert.Equal(AssetState.Ready, entry.State);                  // 尚未迁移（ProcessCompleted 未跑）

        fx.Am.ProcessCompleted();                                    // CommitFrame 第二步
        Assert.Equal(AssetState.Unloaded, entry.State);               // 同帧迁移：顺序契约
    }
}
