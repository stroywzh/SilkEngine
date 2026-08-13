using System.Linq;
using SilkEngine.Core;
using SilkEngine.Scene;

namespace SilkEngine.Tests.Scene;

public class ComponentRegistryTests
{
    private class A : Component { }
    private class B : Component { }
    private class MbA : MonoBehaviour { }
    private class Plain : Component { }

    [Fact]
    public void Register_AddsToCorrectTypeGroup()
    {
        var reg = new ComponentRegistry();
        var a = new GameObject().AddComponent<A>(reg);
        var b = new GameObject().AddComponent<B>(reg);
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
        var a = new GameObject().AddComponent<A>(reg);
        reg.Register(a);
        reg.ApplyPending();
        reg.Unregister(a);
        Assert.Empty(reg.GetOfType<A>());
    }

    [Fact]
    public void RefreshSnapshot_FillsGroups()
    {
        var reg = new ComponentRegistry();
        var a = new GameObject().AddComponent<A>(reg);
        var b = new GameObject().AddComponent<B>(reg);
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
        var a = new GameObject().AddComponent<A>(reg);
        reg.Register(a);
        reg.Unregister(a);
        reg.ApplyPending();
        Assert.Empty(reg.GetOfType<A>());
    }

    [Fact]
    public void Register_DuplicateCall_DoesNotDuplicate()
    {
        var reg = new ComponentRegistry();
        var a = new GameObject().AddComponent<A>(reg);
        reg.Register(a);
        reg.Register(a);
        reg.ApplyPending();
        Assert.Single(reg.GetOfType<A>());
    }

    [Fact]
    public void Register_AfterApplyPending_DoesNotDuplicate()
    {
        var reg = new ComponentRegistry();
        var a = new GameObject().AddComponent<A>(reg);
        reg.Register(a);
        reg.ApplyPending();
        reg.Register(a);
        reg.ApplyPending();
        Assert.Single(reg.GetOfType<A>());
    }

    [Fact]
    public void ApplyPending_IndexesOnlyMonoBehaviours()
    {
        var reg = new ComponentRegistry();
        var a = new GameObject().AddComponent<MbA>(reg);
        var p = new GameObject().AddComponent<Plain>(reg);
        reg.ApplyPending();
        var indexed = reg.MonoBehaviourGroups.SelectMany(g => g).ToList();
        Assert.Equal([a], indexed);   // 仅 MB 入索引；Plain 不入
    }

    [Fact]
    public void Unregister_RemovesFromIndex()
    {
        var reg = new ComponentRegistry();
        var a = new GameObject().AddComponent<MbA>(reg);
        reg.ApplyPending();
        Assert.Single(reg.MonoBehaviourGroups.SelectMany(g => g));
        reg.Unregister(a);
        Assert.Empty(reg.MonoBehaviourGroups.SelectMany(g => g));
    }
}
