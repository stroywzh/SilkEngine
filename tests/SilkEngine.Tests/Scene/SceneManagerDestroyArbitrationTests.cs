using SilkEngine.Core;
using SilkEngine.Scene;
using Object = SilkEngine.Core.Object;

namespace SilkEngine.Tests.Scene;
using Scene = SilkEngine.Scene.Scene;

// 销毁管道统一仲裁（架构 #6）：LoadScene 卸载与 Object.Destroy 共用同一队列 + 帧末 OnDestroy，
// 且双路径对同一对象不产生二次 OnDestroy。
// 串行集合：Object.DestroyHandler 全局静态 + SceneManager ctor 自注册 Services，避免并行污染。
[Collection("SceneManager")]
public class SceneManagerDestroyArbitrationTests : IDisposable
{
    private readonly ComponentRegistry _registry = new();
    private readonly FrameSnapshotManager _snapshotManager = new();
    private readonly SceneManager _manager = new();

    public SceneManagerDestroyArbitrationTests()
    {
        Services.Unregister<SceneManager>(); // 消除 ctor 自注册窗口（本类仅用实例，无 ambient 依赖）
        _manager.Attach(_registry, _snapshotManager);
    }

    /// <summary>测试级清理：注销自注册实例（Unregister 幂等）+ 解绑 DestroyHandler</summary>
    public void Dispose()
    {
        Services.Unregister<SceneManager>();
        _manager.Dispose();
    }

    private sealed class Counter : MonoBehaviour
    {
        public static int OnDestroyCalls;
        public static void Reset() => OnDestroyCalls = 0;
        public override void OnDestroy() => OnDestroyCalls++;
    }

    private sealed class LifecycleCounter : MonoBehaviour
    {
        public List<string> Order { get; } = new();
        public override void OnEnable() => Order.Add("Enable");
        public override void OnDisable() => Order.Add("Disable");
        public override void OnDestroy() => Order.Add("Destroy");
    }

    [Fact]
    public void Destroy_GameObject_FiresOnDisableImmediately_OnDestroyAtFrameEnd()
    {
        var scene = new Scene("s");
        var go = new GameObject("g");
        var c = go.AddComponent<LifecycleCounter>(_registry);
        scene.AddRootObject(go);
        _manager.LoadScene(scene, _registry);
        CommitFrame(scene);

        c.Order.Clear();
        Object.Destroy(go);
        Assert.Equal(["Disable"], c.Order);   // 状态即时：Destroy 调用即失活级联 OnDisable（DestroyRecursive）
        Assert.False(c._destroyed);           // 帧末提交前物理销毁未发生

        CommitFrame(_manager.ActiveScene!);
        Assert.Equal(["Disable", "Destroy"], c.Order);  // 帧末 OnDestroy 在 OnDisable 之后
    }

    private void CommitFrame(Scene active)
    {
        _snapshotManager.CommitPending(_registry, _manager._destroyQueue, active, 0.016f);
    }

    [Fact]
    public void LoadScene_OldScene_DestroysEachObjectOnce()
    {
        Counter.Reset();
        var oldScene = new Scene("old");
        var go = new GameObject("a");
        go.AddComponent<Counter>();
        oldScene.AddRootObject(go);
        _manager.LoadScene(oldScene);
        CommitFrame(oldScene);

        var newScene = new Scene("new");
        _manager.LoadScene(newScene);
        Assert.Equal(0, Counter.OnDestroyCalls); // 立即失活但帧末才物理销毁
        CommitFrame(newScene);
        Assert.Equal(1, Counter.OnDestroyCalls);
    }

    [Fact]
    public void Destroy_ThenLoadScene_SameObject_NoDoubleDestroy()
    {
        Counter.Reset();
        var scene = new Scene("s");
        var go = new GameObject("g");
        go.AddComponent<Counter>();
        scene.AddRootObject(go);
        _manager.LoadScene(scene);
        CommitFrame(scene);

        Object.Destroy(go);
        _manager.LoadScene(new Scene("s2")); // 旧对象已 _destroyPending：LoadScene 不再重复入队

        Assert.Equal(0, Counter.OnDestroyCalls); // 帧末前
        CommitFrame(_manager.ActiveScene!);
        Assert.Equal(1, Counter.OnDestroyCalls);
    }
}
