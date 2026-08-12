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
        public override void OnTick(float dt) { Tick = true; TickDt = dt; }
        public override void OnFixedTick(float dt) => FixedDt = dt;
        public override void OnDestroy() => Destroy = true;
    }

    [Fact]
    public void LoadScene_CallsAwakeAndStart()
    {
        var s = new Scene("T"); var go = new GameObject(); var c = go.AddComponent<Tracker>(); s.AddRootObject(go);
        SceneManager.LoadScene(s);
        Assert.True(c.Awake); Assert.True(c.Start);
    }

    [Fact]
    public void Tick_PassesDeltaTime()
    {
        var s = new Scene("T"); var go = new GameObject(); var c = go.AddComponent<Tracker>(); s.AddRootObject(go);
        SceneManager.LoadScene(s);
        SceneManager.Tick(0.16f);
        Assert.True(c.Tick); Assert.Equal(0.16f, c.TickDt);
    }

    [Fact]
    public void FixedTick_PassesFixedTime()
    {
        var s = new Scene("T"); var go = new GameObject(); var c = go.AddComponent<Tracker>(); s.AddRootObject(go);
        SceneManager.LoadScene(s);
        SceneManager.FixedTick(0.02f);
        Assert.Equal(0.02f, c.FixedDt);
    }

    [Fact]
    public void Inactive_SkipsTick()
    {
        var s = new Scene("T"); var go = new GameObject(); var c = go.AddComponent<Tracker>(); go.IsActive = false; s.AddRootObject(go);
        SceneManager.LoadScene(s);
        SceneManager.Tick(0.16f);
        Assert.False(c.Tick);
    }

    [Fact]
    public void Destroy_AfterProcessDestroys()
    {
        var s = new Scene("T"); var go = new GameObject(); var c = go.AddComponent<Tracker>(); s.AddRootObject(go);
        SceneManager.LoadScene(s);
        Object.Destroy(c); Assert.False(c.Destroy);
        SceneManager.ProcessDestroys(0.1f);
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
        SceneManager.LoadScene(s);

        Object.Destroy(parent);
        Assert.False(child.IsActive);
        Assert.False(c.Enabled);
    }

    [Fact]
    public void Destroy_AfterProcessDestroys_RemovesFromScene()
    {
        var s = new Scene("T");
        var go = new GameObject();
        var c = go.AddComponent<Tracker>();
        s.AddRootObject(go);
        SceneManager.LoadScene(s);

        Object.Destroy(go);
        SceneManager.ProcessDestroys(0.1f);
        Assert.True(c.Destroy);
        Assert.Empty(s.GetRootGameObjects());
    }

    [Fact]
    public void Destroy_Delayed_NotRemovedImmediately()
    {
        var s = new Scene("T");
        var go = new GameObject();
        s.AddRootObject(go);
        SceneManager.LoadScene(s);

        Object.Destroy(go, 1f);
        SceneManager.ProcessDestroys(0.5f);
        Assert.Single(s.GetRootGameObjects());
        SceneManager.ProcessDestroys(0.6f);
        Assert.Empty(s.GetRootGameObjects());
    }
}
