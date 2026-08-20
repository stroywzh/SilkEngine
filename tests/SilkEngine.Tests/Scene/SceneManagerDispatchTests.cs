using SilkEngine;
using SilkEngine.Core;
using SilkEngine.Scene;

namespace SilkEngine.Tests.Scene;
using Scene = SilkEngine.Scene.Scene;

[Collection("SceneManager")]
public class SceneManagerDispatchTests : IDisposable
{
    /// <summary>测试级清理：注销测试内 ctor 自注册的 SceneManager 实例（Unregister 幂等）</summary>
    public void Dispose() => Services.Unregister<SceneManager>();

    private class Tracker : MonoBehaviour
    {
        public int Ticks;
        public override void OnUpdate(float dt) => Ticks++;
    }

    private class DerivedTracker : Tracker { }          // MonoBehaviour 子类（IsSubclassOf 路径）

    private class Plain : Component { }                  // 非 MB：不应被派发扫描

    private static (SceneManager Sm, FrameSnapshotManager Mgr) Setup(
        ComponentRegistry reg, Scene scene, GameObject go)
    {
        var mgr = new FrameSnapshotManager();
        var sm = new SceneManager();
        Services.Unregister<SceneManager>(); // 消除注册窗口（本测试实例自足，无 ambient 依赖）
        sm.Attach(reg, mgr);
        sm.LoadScene(scene, reg);
        mgr.CommitPending(reg, sm._destroyQueue, scene, 0f);
        return (sm, mgr);
    }

    [Fact]
    public void Tick_DispatchesSubclassOfMonoBehaviour()
    {
        var reg = new ComponentRegistry();
        var scene = new Scene("T");
        var go = new GameObject();
        var c = go.AddComponent<DerivedTracker>(reg);
        scene.AddRootObject(go);
        var (sm, mgr) = Setup(reg, scene, go);
        sm.Tick(mgr.Current, 0.016f);
        Assert.Equal(1, c.Ticks);
    }

    [Fact]
    public void Tick_NonMonoBehaviour_IsNotDispatched()
    {
        var reg = new ComponentRegistry();
        var scene = new Scene("T");
        var go = new GameObject();
        var mb = go.AddComponent<Tracker>(reg);
        go.AddComponent<Plain>(reg);
        scene.AddRootObject(go);
        var (sm, mgr) = Setup(reg, scene, go);
        sm.Tick(mgr.Current, 0.016f);
        Assert.Single(reg.GetOfType<Plain>());   // Plain 已注册
        Assert.Equal(1, mb.Ticks);               // 但非 MB 不影响派发，仅 MB 被扫
    }

    [Fact]
    public void Tick_UnregisteredComponent_DispatchedUntilSnapshotRebuild()
    {
        var reg = new ComponentRegistry();
        var scene = new Scene("T");
        var go = new GameObject();
        var c = go.AddComponent<Tracker>(reg);
        scene.AddRootObject(go);
        var (sm, mgr) = Setup(reg, scene, go);

        reg.Unregister(c);
        sm.Tick(mgr.Current, 0.016f);
        Assert.Equal(1, c.Ticks);                // 快照派发：当帧仍按旧快照派发（帧末提交前不生效）

        mgr.CommitPending(reg, sm._destroyQueue, scene, 0f); // 提交新帧 → 快照重建
        sm.Tick(mgr.Current, 0.016f);
        Assert.Equal(1, c.Ticks);                // 新快照已不含 c → 不再派发
    }

    [Fact]
    public void Tick_DispatchOrder_MatchesRegistrationOrder()
    {
        var reg = new ComponentRegistry();
        var scene = new Scene("T");
        var go = new GameObject();
        var order = new List<int>();
        var a = go.AddComponent<Ordered>(reg);
        a.Id = 1;
        a.Order = order;
        var b = go.AddComponent<Ordered>(reg);
        b.Id = 2;
        b.Order = order;
        scene.AddRootObject(go);
        var (sm, mgr) = Setup(reg, scene, go);
        sm.Tick(mgr.Current, 0.016f);
        Assert.Equal([1, 2], order);
    }

    private class Ordered : MonoBehaviour
    {
        public int Id;
        public List<int> Order = null!;
        public override void OnUpdate(float dt) => Order.Add(Id);
    }
}
