using SilkEngine;

namespace SilkEngine.Tests.Scene;

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
}
