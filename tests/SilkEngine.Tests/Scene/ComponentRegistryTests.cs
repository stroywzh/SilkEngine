using System.Diagnostics;
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
    public void BuildSnapshot_FillsGroups()
    {
        var reg = new ComponentRegistry();
        var a = new GameObject().AddComponent<A>(reg);
        var b = new GameObject().AddComponent<B>(reg);
        reg.Register(a);
        reg.Register(b);
        reg.ApplyPending();

        var snap = new FrameSnapshot();
        reg.BuildSnapshot(snap);

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

    [Fact]
    public void Unregister_LastComponent_RemovesEmptyGroup()
    {
        var reg = new ComponentRegistry();
        var a = new GameObject().AddComponent<A>(reg);
        reg.ApplyPending();
        reg.Unregister(a);

        var snap = new FrameSnapshot();
        reg.BuildSnapshot(snap);
        Assert.Empty(snap.Groups);               // 空分组不残留进快照
    }

    [Fact]
    public void Unregister_AfterRegister_RemovesFromReverseIndex()
    {
        var reg = new ComponentRegistry();
        var a = new GameObject().AddComponent<A>(reg);
        reg.ApplyPending();
        Assert.True(reg.Contains(a));            // 已应用 → 索引直查
        reg.Unregister(a);
        Assert.False(reg.Contains(a));           // 注销立即从反查索引摘除
    }

    [Fact]
    public void Contains_PendingRegistration_VisibleBeforeApply()
    {
        var reg = new ComponentRegistry();
        var a = new GameObject().AddComponent<A>(reg);
        Assert.True(reg.Contains(a));            // pending 未应用也可见
        reg.ApplyPending();
        Assert.True(reg.Contains(a));            // 应用后经反向索引直查
    }

    [Fact]
    public void Contains_NotRegistered_ReturnsFalse()
    {
        var reg = new ComponentRegistry();
        Assert.False(reg.Contains(new A()));
    }

    [Fact]
    public void Register_Duplicate_IsIdempotent_AndReverseIndexStaysSingle()
    {
        var reg = new ComponentRegistry();
        var a = new GameObject().AddComponent<A>(reg);
        reg.Register(a);
        reg.Register(a);
        reg.ApplyPending();
        Assert.True(reg.Contains(a));
        Assert.Single(reg.GetOfType<A>());
        reg.Unregister(a);
        Assert.False(reg.Contains(a));
    }

    [Fact]
    public void Unregister_LastOfType_CleansGroupAndReverseIndex()
    {
        var reg = new ComponentRegistry();
        var a1 = new GameObject().AddComponent<A>(reg);
        var a2 = new GameObject().AddComponent<A>(reg);
        reg.ApplyPending();
        reg.Unregister(a1);
        Assert.False(reg.Contains(a1));
        Assert.True(reg.Contains(a2));           // 同组另一组件仍可见
        reg.Unregister(a2);
        Assert.False(reg.Contains(a2));          // 组清空 → 分组与反查索引同步移除
    }

    [Fact]
    public void BulkRegisterUnregister_10k_CompletesWithinTimeout()
    {
        var reg = new ComponentRegistry();
        var items = new Component[10_000];
        for (int i = 0; i < items.Length; i++)
            items[i] = new A();

        var sw = Stopwatch.StartNew();
        foreach (var c in items)
            reg.Register(c);
        reg.ApplyPending();
        foreach (var c in items)
            reg.Unregister(c);
        sw.Stop();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5),
            $"10k 批量注册/注销超时：{sw.Elapsed.TotalSeconds:F1}s");
        Assert.False(reg.Contains(items[0]));
        Assert.Empty(reg.GetOfType<A>());

        foreach (var c in items)
            reg.Register(c);                     // 索引无残留 → 可整体重注册
        reg.ApplyPending();
        Assert.Equal(10_000, reg.GetOfType<A>().Count);
    }
}
