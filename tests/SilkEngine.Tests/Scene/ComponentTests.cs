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
        ml.Tick(0.016f, mgr.Current, reg);
        Assert.False(c.TickCalled);
    }
}
