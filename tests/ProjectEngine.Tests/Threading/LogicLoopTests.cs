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
        public override void OnTick(float dt) => Tick++;
        public override void OnFixedTick(float dt) => Fixed++;
        public override void OnLateTick() => Late++;
        public override void OnPostRender() => Post++;
    }

    [Fact]
    public void Tick_DrivesSceneUpdate()
    {
        var s = new EngineScene("T"); var go = new GameObject(); var c = go.AddComponent<Counter>(); s.AddRootObject(go);
        SceneManager.LoadScene(s);
        var ml = new LogicLoop();
        ml.Tick(0.016f);
        Assert.Equal(1, c.Tick);
    }

    [Fact]
    public void FixedTick_Accumulates()
    {
        var s = new EngineScene("T"); var go = new GameObject(); var c = go.AddComponent<Counter>(); s.AddRootObject(go);
        SceneManager.LoadScene(s);
        var ml = new LogicLoop();
        ml.FixedDeltaTime = 0.02f;
        ml.Tick(0.05f);
        Assert.True(c.Fixed >= 2);
    }

    [Fact]
    public void LateTick_DrivesPostRender()
    {
        var s = new EngineScene("T"); var go = new GameObject(); var c = go.AddComponent<Counter>(); s.AddRootObject(go);
        SceneManager.LoadScene(s);
        var ml = new LogicLoop();
        ml.Tick(0.016f);
        ml.LateTick(0.016f);
        Assert.Equal(1, c.Late);
        Assert.Equal(1, c.Post);
    }
}
