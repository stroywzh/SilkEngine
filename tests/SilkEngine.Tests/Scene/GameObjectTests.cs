using SilkEngine;

namespace SilkEngine.Tests.Scene;
using Scene = SilkEngine.Scene;

[Collection("SceneManager")]
public class GameObjectTests : IClassFixture<SceneManagerFixture>
{
    private readonly SceneManager _sm;

    public GameObjectTests(SceneManagerFixture fixture) => _sm = fixture.Manager;

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
        public bool EnableCalled, Disabled, Destroyed;
        public override void OnEnable() => EnableCalled = true;
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
        _sm._destroyQueue.Clear();
        var reg = new ComponentRegistry();
        var mgr = new FrameSnapshotManager();
        var go = new GameObject();
        var c = go.AddComponent<LifecycleTracker>(reg);
        var scene = new Scene("T");
        scene.AddRootObject(go);
        reg.ApplyPending();
        mgr.CommitPending(reg, _sm._destroyQueue, scene, 0f);

        Assert.True(go.RemoveComponent<LifecycleTracker>(reg));
        Assert.Null(go.GetComponent<LifecycleTracker>());
        Assert.True(c.Disabled);
        Assert.False(c.Destroyed); // 帧末才销毁

        mgr.CommitPending(reg, _sm._destroyQueue, scene, 0f);
        Assert.True(c.Destroyed);
        Assert.Empty(reg.GetOfType<LifecycleTracker>());
    }

    [Fact]
    public void IsActiveInHierarchy_FalseWhenParentInactive()
    {
        var parent = new GameObject();
        var child = new GameObject();
        child.Transform.SetParent(parent.Transform);
        parent.IsActive = false;
        Assert.False(child.IsActiveInHierarchy);
    }

    [Fact]
    public void Deactivate_CascadesDisableToChildren()
    {
        var parent = new GameObject();
        var child = new GameObject();
        child.Transform.SetParent(parent.Transform);
        var pc = parent.AddComponent<LifecycleTracker>();
        var cc = child.AddComponent<LifecycleTracker>();

        parent.IsActive = false;
        Assert.True(pc.Disabled);
        Assert.True(cc.Disabled);
    }

    [Fact]
    public void Deactivate_ThenActivate_FiresEnableAgain()
    {
        var parent = new GameObject();
        var child = new GameObject();
        child.Transform.SetParent(parent.Transform);
        var cc = child.AddComponent<LifecycleTracker>();

        parent.IsActive = false;
        Assert.True(cc.Disabled);

        cc.EnableCalled = false;
        parent.IsActive = true;
        Assert.True(cc.EnableCalled);
    }

    [Fact]
    public void ComponentAddedToInactiveGo_NoEnableUntilActive()
    {
        var go = new GameObject();
        go.IsActive = false;
        var c = go.AddComponent<LifecycleTracker>();
        Assert.False(c.EnableCalled);

        go.IsActive = true;
        Assert.True(c.EnableCalled);
    }

    [Fact]
    public void SetParent_ToInactiveParent_DisablesComponent()
    {
        var parent = new GameObject();
        parent.IsActive = false;
        var child = new GameObject();
        var c = child.AddComponent<LifecycleTracker>();
        Assert.True(c.EnableCalled);   // 无父级时活跃

        child.Transform.SetParent(parent.Transform);
        Assert.True(c.Disabled);       // 转移到失活父级下 → 失活
    }

    [Fact]
    public void CtorWithParent_CascadesDeactivation()
    {
        var parent = new GameObject("P");
        var child = new GameObject(parent.Transform, "C");
        var c = child.AddComponent<LifecycleTracker>();
        Assert.True(c.EnableCalled);

        parent.IsActive = false;
        Assert.True(c.Disabled);
    }

    [Fact]
    public void RemoveComponent_UnderInactiveParent_NoDisableFired()
    {
        _sm._destroyQueue.Clear();
        var parent = new GameObject("P");
        parent.IsActive = false;
        var child = new GameObject(parent.Transform, "C");
        var c = child.AddComponent<LifecycleTracker>();
        Assert.False(c.EnableCalled);   // 父级失活 → 从未 Enable

        Assert.True(child.RemoveComponent<LifecycleTracker>());
        Assert.False(c.Disabled);       // 不应触发无对应的 OnDisable
    }

    [Fact]
    public void Destroy_AfterCommitPending_GetComponentReturnsNull()
    {
        _sm._destroyQueue.Clear();
        var reg = new ComponentRegistry();
        var mgr = new FrameSnapshotManager();
        var scene = new Scene("T");
        var go = new GameObject();
        var c = go.AddComponent<TestComponent>(reg);
        scene.AddRootObject(go);
        reg.ApplyPending();
        mgr.CommitPending(reg, _sm._destroyQueue, scene, 0f);

        Object.Destroy(c);
        mgr.CommitPending(reg, _sm._destroyQueue, scene, 0f);
        Assert.Null(go.GetComponent<TestComponent>());   // 已从 _components 移除
    }

    [Fact]
    public void RemoveComponent_ThenToggleEnabled_NoGhostCallbacks()
    {
        var go = new GameObject();
        var c = go.AddComponent<LifecycleTracker>();
        Assert.True(c.EnableCalled);

        Assert.True(go.RemoveComponent<LifecycleTracker>());
        c.EnableCalled = false;
        c.Disabled = false;

        c.Enabled = false;      // 已移除 → 不应再触发 OnDisable
        Assert.False(c.Disabled);
    }
}
