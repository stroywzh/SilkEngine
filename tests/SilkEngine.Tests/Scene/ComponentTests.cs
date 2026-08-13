using SilkEngine;
using SilkEngine.Threading;

namespace SilkEngine.Tests.Scene;
using Scene = SilkEngine.Scene.Scene;

[Collection("SceneManager")]
public class ComponentTests : IClassFixture<SceneManagerFixture>
{
    private readonly SceneManager _sm;

    public ComponentTests(SceneManagerFixture fixture) => _sm = fixture.Manager;

    private class EnabledTracker : MonoBehaviour
    {
        public bool EnabledCalled, DisabledCalled, TickCalled;
        public override void OnEnable() => EnabledCalled = true;
        public override void OnDisable() => DisabledCalled = true;
        public override void OnUpdate(float dt) => TickCalled = true;
    }

    [Fact]
    public void AddComponent_CallsOnEnable()
    {
        var go = new GameObject();
        var c = go.AddComponent<EnabledTracker>();
        Assert.True(c.EnabledCalled);
    }

    [Fact]
    public void SetEnabledFalse_CallsOnDisable()
    {
        var go = new GameObject();
        var c = go.AddComponent<EnabledTracker>();
        c.Enabled = false;
        Assert.True(c.DisabledCalled);
    }

    [Fact]
    public void SetEnabledTrue_CallsOnEnable()
    {
        var go = new GameObject();
        var c = go.AddComponent<EnabledTracker>();
        c.Enabled = false;
        c.EnabledCalled = false;
        c.Enabled = true;
        Assert.True(c.EnabledCalled);
    }

    [Fact]
    public void DisabledComponent_SkippedByTick()
    {
        var s = new Scene("T"); var go = new GameObject(); var c = go.AddComponent<EnabledTracker>();
        s.AddRootObject(go);
        var reg = new ComponentRegistry();
        var mgr = new FrameSnapshotManager();
        _sm.LoadScene(s, reg);
        mgr.CommitPending(reg, new List<SceneManager.DestroyEntry>(), s, 0f);
        c.Enabled = false;
        var ml = new LogicLoop(_sm);
        ml.Tick(0.016f, mgr.Current);
        Assert.False(c.TickCalled);
    }

    private class PlainComponent : Component
    {
        public bool EnableCalled, DisableCalled, DestroyCalled;
        public override void OnEnable() => EnableCalled = true;
        public override void OnDisable() => DisableCalled = true;
        public override void OnDestroy() => DestroyCalled = true;
    }

    [Fact]
    public void PlainComponent_GetsEnableDisableCallbacks()
    {
        var go = new GameObject();
        var c = go.AddComponent<PlainComponent>();
        Assert.True(c.EnableCalled);   // AddComponent 时触发 OnEnable（当前只对 MonoBehaviour 触发）

        c.Enabled = false;
        Assert.True(c.DisableCalled);

        c.EnableCalled = false;
        c.Enabled = true;
        Assert.True(c.EnableCalled);
    }

    [Fact]
    public void SetDisabledBeforeFirstEnable_DoesNotFireDisable()
    {
        var go = new GameObject();
        var c = new PlainComponent();               // 绕过 AddComponent，模拟"尚未初始化"组件
        go._components.Add(c);
        c.GameObject = go;

        c.Enabled = false;                          // 从未 Enable 过
        Assert.False(c.DisableCalled);

        c.Enabled = true;                           // 首次
        Assert.True(c.EnableCalled);

        c.Enabled = false;                          // 已 Enable → OnDisable
        Assert.True(c.DisableCalled);
    }

    private class AwakeTracker : MonoBehaviour
    {
        public bool AwakeCalled;
        public override void OnAwake() => AwakeCalled = true;
    }

    [Fact]
    public void AddComponent_CallsAwakeImmediately()
    {
        var go = new GameObject();
        var c = go.AddComponent<AwakeTracker>();
        Assert.True(c.AwakeCalled);   // 实例初始化后立即（不依赖 LoadScene）
    }

    [Fact]
    public void AddComponent_AwakeBeforeEnable()
    {
        var go = new GameObject();
        var c = go.AddComponent<OrderedLifecycle>();
        Assert.Equal(["Awake", "Enable"], c.Order);
    }

    private class OrderedLifecycle : MonoBehaviour
    {
        public List<string> Order { get; } = new();
        public override void OnAwake() => Order.Add("Awake");
        public override void OnEnable() => Order.Add("Enable");
    }

    [Fact]
    public void Awake_SetsEnabledFalse_NoEnableNoDisable()
    {
        var go = new GameObject();
        var c = go.AddComponent<SelfDisabler>();
        Assert.False(c.EnableCalled);
        Assert.False(c.DisableCalled);
        Assert.True(c.AwakeCalled);

        c.Enabled = true;               // 后启用
        Assert.True(c.EnableCalled);
    }

    private class SelfDisabler : MonoBehaviour
    {
        public bool AwakeCalled, EnableCalled, DisableCalled;
        public override void OnAwake() { AwakeCalled = true; Enabled = false; }
        public override void OnEnable() => EnableCalled = true;
        public override void OnDisable() => DisableCalled = true;
    }

    [Fact]
    public void LoadScene_DoesNotDoubleAwake()
    {
        var go = new GameObject();
        var c = go.AddComponent<AwakeCounter>();
        var s = new Scene("T");
        s.AddRootObject(go);
        var reg = new ComponentRegistry();
        c.AwakeCount = 0;

        _sm.LoadScene(s, reg);
        Assert.Equal(0, c.AwakeCount);   // LoadScene 不再触发 Awake（工厂已保证）
    }

    private class AwakeCounter : MonoBehaviour
    {
        public int AwakeCount;
        public override void OnAwake() => AwakeCount++;
    }
}
