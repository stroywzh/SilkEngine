using SilkEngine;
using SilkEngine.Threading;

namespace SilkEngine.Tests.Scene;
using Scene = SilkEngine.Scene;

[Collection("SceneManager")]
public class ComponentTests
{
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
        SceneManager.Instance.LoadScene(s, reg);
        mgr.CommitPending(reg, new List<SceneManager.DestroyEntry>(), s, 0f);
        c.Enabled = false;
        var ml = new LogicLoop();
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
}
