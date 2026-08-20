using SilkEngine.Core;
using SilkEngine.Scene;
using Object = SilkEngine.Core.Object;

namespace SilkEngine.Tests.Core;

using Scene = SilkEngine.Scene.Scene;

[Collection("SceneManager")]
public class CommitFrameOrderTests : IDisposable
{
    /// <summary>测试级清理：注销测试内 ctor 自注册的 SceneManager 实例（Unregister 幂等）</summary>
    public void Dispose() => Services.Unregister<SceneManager>();

    // 局部夹具：订阅 DestroyHandler 到被测实例的 _destroyQueue（引擎流由 EngineLoop 订阅）
    private sealed class Fixture : IDisposable
    {
        public ComponentRegistry Reg = new();
        public FrameSnapshotManager Mgr = new();
        public SceneManager Sm = new();

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

    private class Spawner : MonoBehaviour
    {
        public GameObject? SpawnTarget;
        public ComponentRegistry? TargetRegistry;
        public bool Destroyed;
        public override void OnDestroy()
        {
            Destroyed = true;
            SpawnTarget!.AddComponent<DestroyTracker>(TargetRegistry); // 销毁处理期注册 → 进 pending
        }
    }

    private class DestroyTracker : MonoBehaviour { }

    [Fact]
    public void CommitFrame_OnDestroyRegistrations_VisibleInNextSnapshot()
    {
        using var fx = new Fixture();
        var scene = new Scene("T");
        var go = new GameObject();
        var spawner = go.AddComponent<Spawner>(fx.Reg);
        var fresh = new GameObject("Fresh");
        spawner.SpawnTarget = fresh;
        spawner.TargetRegistry = fx.Reg;
        scene.AddRootObject(go);
        fx.Reg.ApplyPending();
        fx.Mgr.CommitPending(fx.Reg, fx.Sm._destroyQueue, scene, 0f);

        Object.Destroy(go);
        fx.Mgr.CommitPending(fx.Reg, fx.Sm._destroyQueue, scene, 0f); // CommitFrame 第一步

        Assert.True(spawner.Destroyed);
        Assert.Contains(
            fresh.GetComponent<DestroyTracker>(),
            fx.Mgr.Current.GetComponents<DestroyTracker>());          // 销毁→注册应用顺序：新组件进下一帧快照
    }
}
