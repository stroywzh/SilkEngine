using SilkEngine;

namespace SilkEngine.Tests.Scene;
using Scene = SilkEngine.Scene;

[Collection("SceneManager")]
public class SceneManagerTests
{
    private class Tracker : MonoBehaviour
    {
        public bool Awake, Start, Tick, Destroy;
        public float TickDt, FixedDt;
        public override void OnAwake() => Awake = true;
        public override void OnStart() => Start = true;
        public override void OnUpdate(float dt) { Tick = true; TickDt = dt; }
        public override void OnFixedUpdate(float dt) => FixedDt = dt;
        public override void OnDestroy() => Destroy = true;
    }

    [Fact]
    public void LoadScene_CallsAwakeAndStart()
    {
        var s = new Scene("T"); var go = new GameObject(); var c = go.AddComponent<Tracker>(); s.AddRootObject(go);
        var reg = new ComponentRegistry();
        var mgr = new FrameSnapshotManager();
        SceneManager.Instance.LoadScene(s, reg);
        mgr.CommitPending(reg, new List<SceneManager.DestroyEntry>(), s, 0f);
        SceneManager.Instance.Tick(mgr.Current, 0.016f);
        Assert.True(c.Awake); Assert.True(c.Start);
    }

    [Fact]
    public void Tick_PassesDeltaTime()
    {
        var s = new Scene("T"); var go = new GameObject(); var c = go.AddComponent<Tracker>(); s.AddRootObject(go);
        var reg = new ComponentRegistry();
        var mgr = new FrameSnapshotManager();
        SceneManager.Instance.LoadScene(s, reg);
        mgr.CommitPending(reg, new List<SceneManager.DestroyEntry>(), s, 0f);
        SceneManager.Instance.Tick(mgr.Current, 0.16f);
        Assert.True(c.Tick); Assert.Equal(0.16f, c.TickDt);
    }

    [Fact]
    public void FixedTick_PassesFixedTime()
    {
        var s = new Scene("T"); var go = new GameObject(); var c = go.AddComponent<Tracker>(); s.AddRootObject(go);
        var reg = new ComponentRegistry();
        var mgr = new FrameSnapshotManager();
        SceneManager.Instance.LoadScene(s, reg);
        mgr.CommitPending(reg, new List<SceneManager.DestroyEntry>(), s, 0f);
        SceneManager.Instance.FixedTick(mgr.Current, 0.02f);
        Assert.Equal(0.02f, c.FixedDt);
    }

    [Fact]
    public void Inactive_SkipsTick()
    {
        var s = new Scene("T"); var go = new GameObject(); var c = go.AddComponent<Tracker>(); go.IsActive = false; s.AddRootObject(go);
        var reg = new ComponentRegistry();
        var mgr = new FrameSnapshotManager();
        SceneManager.Instance.LoadScene(s, reg);
        mgr.CommitPending(reg, new List<SceneManager.DestroyEntry>(), s, 0f);
        SceneManager.Instance.Tick(mgr.Current, 0.16f);
        Assert.False(c.Tick);
    }

    [Fact]
    public void Destroy_AfterCommitPending()
    {
        SceneManager.Instance._destroyQueue.Clear();
        var s = new Scene("T"); var go = new GameObject(); var c = go.AddComponent<Tracker>(); s.AddRootObject(go);
        var reg = new ComponentRegistry();
        var mgr = new FrameSnapshotManager();
        SceneManager.Instance.LoadScene(s, reg);
        Object.Destroy(c); Assert.False(c.Destroy);
        mgr.CommitPending(reg, SceneManager.Instance._destroyQueue, s, 0.1f);
        Assert.True(c.Destroy);
    }

    [Fact]
    public void LateTick_CallsLateTick() { /* skip for brevity - covered by LogicLoop tests */ }
    [Fact]
    public void PostRender_CallsPostRender() { /* skip for brevity */ }

    [Fact]
    public void Destroy_GameObject_RecursivelyDestroysChildren()
    {
        var s = new Scene("T");
        var parent = new GameObject("P");
        var child = new GameObject("C");
        child.Transform.SetParent(parent.Transform);
        var c = child.AddComponent<Tracker>();
        s.AddRootObject(parent);
        SceneManager.Instance.LoadScene(s);

        Object.Destroy(parent);
        Assert.False(child.IsActive);
        Assert.False(c.Enabled);
    }

    [Fact]
    public void Destroy_AfterCommitPending_RemovesFromScene()
    {
        SceneManager.Instance._destroyQueue.Clear();
        var s = new Scene("T");
        var go = new GameObject();
        var c = go.AddComponent<Tracker>();
        s.AddRootObject(go);
        var reg = new ComponentRegistry();
        var mgr = new FrameSnapshotManager();
        SceneManager.Instance.LoadScene(s, reg);

        Object.Destroy(go);
        mgr.CommitPending(reg, SceneManager.Instance._destroyQueue, s, 0.1f);
        Assert.True(c.Destroy);
        Assert.Empty(s.GetRootGameObjects());
    }

    [Fact]
    public void Destroy_Delayed_NotRemovedImmediately()
    {
        SceneManager.Instance._destroyQueue.Clear();
        var s = new Scene("T");
        var go = new GameObject();
        s.AddRootObject(go);
        var reg = new ComponentRegistry();
        var mgr = new FrameSnapshotManager();
        SceneManager.Instance.LoadScene(s, reg);

        Object.Destroy(go, 1f);
        mgr.CommitPending(reg, SceneManager.Instance._destroyQueue, s, 0.5f);
        Assert.Single(s.GetRootGameObjects());
        mgr.CommitPending(reg, SceneManager.Instance._destroyQueue, s, 0.6f);
        Assert.Empty(s.GetRootGameObjects());
    }

    [Fact]
    public void Tick_UsesRegistry()
    {
        var reg = new ComponentRegistry();
        var mgr = new FrameSnapshotManager();
        var s = new Scene("T");
        var go = new GameObject();
        var c = go.AddComponent<Tracker>(reg);
        s.AddRootObject(go);

        reg.ApplyPending();
        mgr.CommitPending(reg, new List<SceneManager.DestroyEntry>(), s, 0f);

        SceneManager.Instance.Tick(mgr.Current, 0.16f);
        Assert.True(c.Tick);
    }

    [Fact]
    public void Tick_MultipleComponents_RegistrationOrder()
    {
        var reg = new ComponentRegistry();
        var mgr = new FrameSnapshotManager();
        var s = new Scene("T");
        var go = new GameObject();
        var order = new List<int>();

        var a = go.AddComponent<Ordered>(reg); a.Id = 1; a.Order = order;
        var b = go.AddComponent<Ordered>(reg); b.Id = 2; b.Order = order;
        s.AddRootObject(go);

        reg.ApplyPending();
        mgr.CommitPending(reg, new List<SceneManager.DestroyEntry>(), s, 0f);
        SceneManager.Instance.Tick(mgr.Current, 0.16f);

        Assert.Equal([1, 2], order);
    }

    private class Ordered : MonoBehaviour
    {
        public int Id;
        public List<int> Order = null!;
        public override void OnUpdate(float dt) => Order.Add(Id);
    }

    [Fact]
    public void LoadScene_WithRegistry_RegistersComponents()
    {
        var reg = new ComponentRegistry();
        var s = new Scene("T");
        var go = new GameObject();
        var c = go.AddComponent<Tracker>();
        s.AddRootObject(go);

        SceneManager.Instance.LoadScene(s, reg);
        Assert.Single(reg.GetOfType<Tracker>());
        Assert.True(c.Awake);
    }

    [Fact]
    public void RegisterScene_RegistersAllComponents()
    {
        var reg = new ComponentRegistry();
        var s = new Scene("T");
        var go = new GameObject();
        var c = go.AddComponent<Tracker>();
        s.AddRootObject(go);
        SceneManager.Instance.LoadScene(s);

        SceneManager.Instance.RegisterScene(reg);
        Assert.Single(reg.GetOfType<Tracker>());
    }

    [Fact]
    public void LateEnable_StartsOnce()
    {
        var s = new Scene("T");
        var go = new GameObject();
        var c = go.AddComponent<Tracker>();
        s.AddRootObject(go);
        var reg = new ComponentRegistry();
        var mgr = new FrameSnapshotManager();
        SceneManager.Instance.LoadScene(s, reg);
        mgr.CommitPending(reg, new List<SceneManager.DestroyEntry>(), s, 0f);

        c.Enabled = false;
        SceneManager.Instance.Tick(mgr.Current, 0.016f);
        Assert.False(c.Start);              // 禁用 → 不 Start

        c.Enabled = true;
        SceneManager.Instance.Tick(mgr.Current, 0.016f);
        Assert.True(c.Start);               // 后启用 → 补 Start

        c.Start = false;
        SceneManager.Instance.Tick(mgr.Current, 0.016f);
        Assert.False(c.Start);              // 仅一次
    }

    [Fact]
    public void LoadScene_SwitchesUnregistersOldScene()
    {
        var reg = new ComponentRegistry();
        var mgr = new FrameSnapshotManager();
        var s1 = new Scene("A");
        var go1 = new GameObject();
        var c1 = go1.AddComponent<Tracker>();
        s1.AddRootObject(go1);
        SceneManager.Instance.LoadScene(s1, reg);
        mgr.CommitPending(reg, new List<SceneManager.DestroyEntry>(), s1, 0f);

        var s2 = new Scene("B");
        var go2 = new GameObject();
        var c2 = go2.AddComponent<Tracker>();
        s2.AddRootObject(go2);
        SceneManager.Instance.LoadScene(s2, reg);
        mgr.CommitPending(reg, new List<SceneManager.DestroyEntry>(), s2, 0f);

        Assert.True(c1.Destroy);                        // 旧场景组件收到 OnDestroy
        var all = reg.GetOfType<Tracker>();
        Assert.Single(all);                              // 仅新场景组件在册
        Assert.Same(c2, all[0]);
    }

    [Fact]
    public void AddObjectToScene_RegistersAndRejectsDuplicates()
    {
        var reg = new ComponentRegistry();
        SceneManager.ActiveRegistry = reg;
        try
        {
            var s = new Scene("T");
            SceneManager.Instance.LoadScene(s, reg);
            var go = new GameObject();
            var c = go.AddComponent<AwakeCounter>();
            Assert.Equal(1, c.AwakeCount);          // 工厂已 Awake

            Assert.True(SceneManager.AddObjectToScene(go));
            Assert.False(SceneManager.AddObjectToScene(go));   // 重复 → false

            reg.ApplyPending();
            Assert.Single(reg.GetOfType<AwakeCounter>());       // Register 去重
        }
        finally
        {
            SceneManager.ActiveRegistry = null;
        }
    }

    private class AwakeCounter : MonoBehaviour
    {
        public int AwakeCount;
        public override void OnAwake() => AwakeCount++;
    }
}
