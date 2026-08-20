using SilkEngine.Core;
using SilkEngine.Scene;

namespace SilkEngine.Tests.Scene;
using Scene = SilkEngine.Scene.Scene;
using Object = SilkEngine.Core.Object;

// 派发读快照（架构 #4）：Tick/FixedTick/LateTick/PostRender 遍历 FrameSnapshot 的复制型
// MbGroups，而非实时注册表索引——回调内 LoadScene 不再使派发枚举崩溃。
// 串行集合：Object.DestroyHandler 全局静态 + SceneManager ctor 自注册 Services，避免并行污染。
[Collection("SceneManager")]
public class SceneManagerSnapshotDispatchTests : IDisposable
{
    /// <summary>测试级清理：注销测试内 ctor 自注册的 SceneManager 实例（Unregister 幂等）</summary>
    public void Dispose() => Services.Unregister<SceneManager>();

    private sealed class SceneSwitcher : MonoBehaviour
    {
        public static SceneManager? Manager;
        public static Scene? Other;
        public override void OnUpdate(float dt)
        {
            if (Manager != null && Other != null)
            {
                var m = Manager;
                var o = Other;
                Manager = null;
                Other = null;
                m.LoadScene(o); // 回调内 LoadScene：原实现枚举实时注册表 → 崩溃
            }
        }
    }

    [Fact]
    public void LoadScene_InsideComponentCallback_DoesNotThrow()
    {
        var registry = new ComponentRegistry();
        var snapshotManager = new FrameSnapshotManager();
        using var sceneManager = new SceneManager();
        sceneManager.Attach(registry, snapshotManager);

        var sceneA = new Scene("A");
        var go = new GameObject("g");
        go.AddComponent<SceneSwitcher>();
        sceneA.AddRootObject(go);
        sceneManager.LoadScene(sceneA);
        snapshotManager.CommitPending(registry, sceneManager._destroyQueue, sceneA, 0f); // 先提交：快照含 SceneSwitcher

        var sceneB = new Scene("B");
        var goB = new GameObject("gB");
        goB.AddComponent<EmptyMb>();
        sceneB.AddRootObject(goB);
        SceneSwitcher.Manager = sceneManager;
        SceneSwitcher.Other = sceneB;

        var ex = Record.Exception(() => sceneManager.Tick(snapshotManager.Current, 0.016f));
        Assert.Null(ex);
        Assert.Same(sceneB, sceneManager.ActiveScene);
    }

    [Fact]
    public void Snapshot_IsIsolated_FromLiveRegistry()
    {
        var registry = new ComponentRegistry();
        var snapshotManager = new FrameSnapshotManager();
        using var sceneManager = new SceneManager();
        sceneManager.Attach(registry, snapshotManager);

        var scene = new Scene("s");
        var go = new GameObject("g");
        go.AddComponent<EmptyMb>();
        scene.AddRootObject(go);
        sceneManager.LoadScene(scene);
        snapshotManager.CommitPending(registry, sceneManager._destroyQueue, scene, 0f);

        var before = snapshotManager.Current;
        Object.Destroy(go);
        Assert.Same(before, snapshotManager.Current); // 帧末前快照不换
        Assert.Contains(before.MbGroups, list => list.Any(mb => mb.GameObject == go));
    }

    private sealed class EmptyMb : MonoBehaviour { }
}
