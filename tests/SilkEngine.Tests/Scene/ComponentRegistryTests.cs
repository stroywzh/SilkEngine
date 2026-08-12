using SilkEngine;

namespace SilkEngine.Tests.Scene;

public class ComponentRegistryTests
{
    private class A : Component { }
    private class B : Component { }

    [Fact]
    public void Register_AddsToCorrectTypeGroup()
    {
        var reg = new ComponentRegistry();
        var a = new GameObject().AddComponent<A>();
        var b = new GameObject().AddComponent<B>();
        reg.Register(a);
        reg.Register(b);
        reg.ApplyPending();
        Assert.Single(reg.GetOfType<A>());
        Assert.Single(reg.GetOfType<B>());
        Assert.Same(a, reg.GetOfType<A>()[0]);
    }

    [Fact]
    public void Unregister_RemovesComponent()
    {
        var reg = new ComponentRegistry();
        var a = new GameObject().AddComponent<A>();
        reg.Register(a);
        reg.ApplyPending();
        reg.Unregister(a);
        Assert.Empty(reg.GetOfType<A>());
    }

    [Fact]
    public void RefreshSnapshot_FillsGroups()
    {
        var reg = new ComponentRegistry();
        var a = new GameObject().AddComponent<A>();
        var b = new GameObject().AddComponent<B>();
        reg.Register(a);
        reg.Register(b);
        reg.ApplyPending();

        var snap = new FrameSnapshot();
        reg.RefreshSnapshot(snap);

        Assert.Equal(2, snap.Groups.Count);
        Assert.Single(snap.GetComponents<A>());
        Assert.Single(snap.GetComponents<B>());
    }

    [Fact]
    public void GetOfType_EmptyRegistry_ReturnsEmpty()
    {
        var reg = new ComponentRegistry();
        Assert.Empty(reg.GetOfType<A>());
    }

    [Fact]
    public void Unregister_BeforeApplyPending_RemovesFromPending()
    {
        var reg = new ComponentRegistry();
        var a = new GameObject().AddComponent<A>();
        reg.Register(a);
        reg.Unregister(a);
        reg.ApplyPending();
        Assert.Empty(reg.GetOfType<A>());
    }
}
