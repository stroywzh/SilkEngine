using SilkEngine;
using SilkEngine.Core;
using SilkEngine.Scene;

namespace SilkEngine.Tests.Core;

using Scene = SilkEngine.Scene.Scene;

[Collection("SceneManager")]
public class EngineLoopTickOrchestrationTests : IDisposable
{
    /// <summary>测试级清理：注销测试内 ctor 自注册的 SceneManager 实例（Unregister 幂等）</summary>
    public void Dispose() => Services.Unregister<SceneManager>();

    private class Counter : MonoBehaviour
    {
        public int Tick, Fixed, Late, Post;
        public float FixedDt;
        public override void OnUpdate(float dt) => Tick++;
        public override void OnFixedUpdate(float dt) { Fixed++; FixedDt = dt; }
        public override void OnLateUpdate() => Late++;
        public override void OnPostRender() => Post++;
    }

    private static (FixedStepAccumulator Acc, Counter C, SceneManager Sm, FrameSnapshotManager Mgr) Setup()
    {
        var reg = new ComponentRegistry();
        var mgr = new FrameSnapshotManager();
        var sm = new SceneManager();
        Services.Unregister<SceneManager>(); // 消除注册窗口（本测试实例自足，无 ambient 依赖）
        sm.Attach(reg, mgr);
        var s = new Scene("T");
        var go = new GameObject();
        var c = go.AddComponent<Counter>(reg);
        s.AddRootObject(go);
        sm.LoadScene(s, reg);
        mgr.CommitPending(reg, sm._destroyQueue, s, 0f);
        return (new FixedStepAccumulator(), c, sm, mgr);
    }

    // 与 EngineLoop.TickFrame 逐行相同的调用序列（EngineLoop.Run 依赖渲染后端，不可整体单测）
    private static void TickFrame(FixedStepAccumulator acc, SceneManager sm, FrameSnapshot snap, float dt)
    {
        int steps = acc.Advance(dt);
        for (int i = 0; i < steps; i++)
            sm.FixedTick(snap, acc.FixedDeltaTime);
        sm.Tick(snap, dt);
        sm.LateTick(snap);
    }

    [Fact]
    public void TickFrame_DrivesSceneUpdate()
    {
        var (acc, c, sm, mgr) = Setup();
        TickFrame(acc, sm, mgr.Current, 0.016f);
        Assert.Equal(1, c.Tick);
        Assert.Equal(0, c.Fixed);
    }

    [Fact]
    public void TickFrame_AccumulatesFixedSteps()
    {
        var (acc, c, sm, mgr) = Setup();
        TickFrame(acc, sm, mgr.Current, 0.05f);
        Assert.Equal(2, c.Fixed);                 // 0.05 = 2×0.02 + 余 0.01
        Assert.Equal(0.02f, c.FixedDt);           // 固定步长值传入 FixedTick
        Assert.Equal(1, c.Tick);
    }

    [Fact]
    public void TickFrame_TwoFramesOfSixteenMs_TriggersOneFixed()
    {
        var (acc, c, sm, mgr) = Setup();
        TickFrame(acc, sm, mgr.Current, 0.016f);
        Assert.Equal(0, c.Fixed);
        TickFrame(acc, sm, mgr.Current, 0.016f);
        Assert.Equal(1, c.Fixed);                 // 0.032 跨过 0.02 → 一次
        Assert.Equal(0.012f, acc.Remainder, 5);   // 剩余累积保留
    }

    [Fact]
    public void TickFrame_ThenPostRender_DrivesLateAndPost()
    {
        var (acc, c, sm, mgr) = Setup();
        TickFrame(acc, sm, mgr.Current, 0.016f);
        sm.PostRender(mgr.Current);               // EngineLoop.Run 中 Render 之后的调用
        Assert.Equal(1, c.Late);
        Assert.Equal(1, c.Post);
    }

    [Fact]
    public void TickFrame_DrivesRegistryComponents()
    {
        var reg = new ComponentRegistry();
        var mgr = new FrameSnapshotManager();
        var sm = new SceneManager();
        Services.Unregister<SceneManager>(); // 消除注册窗口（本测试实例自足，无 ambient 依赖）
        sm.Attach(reg, mgr);
        var s = new Scene("T");
        var go = new GameObject();
        var c = go.AddComponent<Counter>(reg);
        s.AddRootObject(go);
        reg.ApplyPending();
        mgr.CommitPending(reg, sm._destroyQueue, s, 0f);

        TickFrame(new FixedStepAccumulator(), sm, mgr.Current, 0.016f);
        Assert.Equal(1, c.Tick);
    }
}
