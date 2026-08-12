using SilkEngine;

namespace SilkEngine.Tests.Scene;
using Scene = SilkEngine.Scene;

[Collection("SceneManager")]
public class GameObjectTests
{
    private class TestComponent : Component { }

    [Fact] public void HasTransform() => Assert.NotNull(new GameObject().Transform);
    [Fact] public void DefaultName() => Assert.Equal("GameObject", new GameObject().Name);
    [Fact] public void CustomName() => Assert.Equal("Player", new GameObject("Player").Name);
    [Fact] public void IsActive_DefaultsTrue() => Assert.True(new GameObject().IsActive);

    [Fact]
    public void AddComponent_ReturnsAndAssigns()
    {
        var go = new GameObject();
        var c = go.AddComponent<TestComponent>();
        Assert.NotNull(c);
        Assert.Same(go, c.GameObject);
    }

    [Fact]
    public void GetComponent_FindsAdded()
    {
        var go = new GameObject();
        go.AddComponent<TestComponent>();
        Assert.NotNull(go.GetComponent<TestComponent>());
    }

    [Fact]
    public void RemoveComponent_Removes()
    {
        var go = new GameObject();
        go.AddComponent<TestComponent>();
        Assert.True(go.RemoveComponent<TestComponent>());
        Assert.Null(go.GetComponent<TestComponent>());
    }

    [Fact]
    public void Transform_FromComponent()
    {
        var go = new GameObject();
        var c = go.AddComponent<TestComponent>();
        Assert.Same(go.Transform, c.Transform);
    }

    [Fact]
    public void AddComponent_WithRegistry_GoesToPending()
    {
        var reg = new ComponentRegistry();
        var go = new GameObject();
        var c = go.AddComponent<TestComponent>(reg);
        Assert.Empty(reg.GetOfType<TestComponent>());

        reg.ApplyPending();
        Assert.Single(reg.GetOfType<TestComponent>());
        Assert.Same(c, reg.GetOfType<TestComponent>()[0]);
    }

    [Fact]
    public void AddComponent_WithoutRegistry_StillWorks()
    {
        var go = new GameObject();
        var c = go.AddComponent<TestComponent>();
        Assert.NotNull(c);
        Assert.Same(go, c.GameObject);
    }

    private class LifecycleTracker : MonoBehaviour
    {
        public bool Disabled, Destroyed;
        public override void OnDisable() => Disabled = true;
        public override void OnDestroy() => Destroyed = true;
    }

    [Fact]
    public void AddComponent_AmbientRegistry_AutoRegisters()
    {
        var reg = new ComponentRegistry();
        SceneManager.ActiveRegistry = reg;
        try
        {
            var go = new GameObject();
            var c = go.AddComponent<TestComponent>();
            reg.ApplyPending();
            Assert.Single(reg.GetOfType<TestComponent>());
            Assert.Same(c, reg.GetOfType<TestComponent>()[0]);
        }
        finally
        {
            SceneManager.ActiveRegistry = null;
        }
    }

    [Fact]
    public void RemoveComponent_CallsDisableAndDefersDestroy()
    {
        SceneManager._destroyQueue.Clear();
        var reg = new ComponentRegistry();
        var mgr = new FrameSnapshotManager();
        var go = new GameObject();
        var c = go.AddComponent<LifecycleTracker>(reg);
        var scene = new Scene("T");
        scene.AddRootObject(go);
        reg.ApplyPending();
        mgr.CommitPending(reg, SceneManager._destroyQueue, scene, 0f);

        Assert.True(go.RemoveComponent<LifecycleTracker>(reg));
        Assert.Null(go.GetComponent<LifecycleTracker>());
        Assert.True(c.Disabled);
        Assert.False(c.Destroyed); // 帧末才销毁

        mgr.CommitPending(reg, SceneManager._destroyQueue, scene, 0f);
        Assert.True(c.Destroyed);
        Assert.Empty(reg.GetOfType<LifecycleTracker>());
    }
}
