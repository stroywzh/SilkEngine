using SilkEngine;
using SilkEngine.Threading;

namespace SilkEngine.Tests;
using EngineScene = SilkEngine.Scene;

[Collection("SceneManager")]
public class LogicLoopTests
{
    private class Counter : MonoBehaviour
    {
        public int Tick, Fixed, Late, Post;
        public override void OnUpdate(float dt) => Tick++;
        public override void OnFixedUpdate(float dt) => Fixed++;
        public override void OnLateUpdate() => Late++;
        public override void OnPostRender() => Post++;
    }

    private static (LogicLoop Loop, Counter Counter, ComponentRegistry Reg, FrameSnapshotManager Mgr) Setup()
    {
        var s = new EngineScene("T"); var go = new GameObject(); var c = go.AddComponent<Counter>(); s.AddRootObject(go);
        var reg = new ComponentRegistry();
        var mgr = new FrameSnapshotManager();
        SceneManager.Instance.LoadScene(s, reg);
        mgr.CommitPending(reg, new List<SceneManager.DestroyEntry>(), s, 0f);
        return (new LogicLoop(), c, reg, mgr);
    }

    [Fact]
    public void Tick_DrivesSceneUpdate()
    {
        var (ml, c, reg, mgr) = Setup();
        ml.Tick(0.016f, mgr.Current);
        Assert.Equal(1, c.Tick);
    }

    [Fact]
    public void FixedTick_Accumulates()
    {
        var (ml, c, reg, mgr) = Setup();
        ml.FixedDeltaTime = 0.02f;
        ml.Tick(0.05f, mgr.Current);
        Assert.True(c.Fixed >= 2);
    }

    [Fact]
    public void LateTick_DrivesPostRender()
    {
        var (ml, c, reg, mgr) = Setup();
        ml.Tick(0.016f, mgr.Current);
        ml.LateTick(0.016f, mgr.Current);
        Assert.Equal(1, c.Late);
        Assert.Equal(1, c.Post);
    }

    [Fact]
    public void Tick_DrivesRegistryComponents()
    {
        var reg = new ComponentRegistry();
        var mgr = new FrameSnapshotManager();
        var s = new EngineScene("T");
        var go = new GameObject();
        var c = go.AddComponent<Counter>(reg);
        s.AddRootObject(go);

        reg.ApplyPending();
        mgr.CommitPending(reg, new List<SceneManager.DestroyEntry>(), s, 0f);

        var ml = new LogicLoop();
        ml.Tick(0.016f, mgr.Current);
        Assert.Equal(1, c.Tick);
    }
}
