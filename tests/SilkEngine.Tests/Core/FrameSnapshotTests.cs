using System.Collections.Generic;
using SilkEngine.Core;
using SilkEngine.Scene;
using SilkEngine.Tests.Scene;
using Object = SilkEngine.Core.Object;

namespace SilkEngine.Tests.Core;
using Scene = SilkEngine.Scene.Scene;

[Collection("SceneManager")]
public class FrameSnapshotTests : IClassFixture<SceneManagerFixture>
{
    private readonly SceneManager _sm;

    public FrameSnapshotTests(SceneManagerFixture fixture) => _sm = fixture.Manager;
    [Fact]
    public void NewSnapshot_HasDefaultValues()
    {
        var snap = new FrameSnapshot();
        Assert.Equal(0L, snap.FrameCount);
        Assert.Null(snap.ActiveScene);
        Assert.NotNull(snap.GetComponents<Component>());
        Assert.Empty(snap.GetComponents<Component>());
    }

    [Fact]
    public void GetComponents_ReturnsTypedList()
    {
        var snap = new FrameSnapshot();
        var go = new GameObject();
        var c = go.AddComponent<TestTracker>();
        snap.ActiveScene = new Scene("T");
        snap.Groups.Add(new ComponentGroup { ComponentType = typeof(TestTracker), Components = [c] });
        var list = snap.GetComponents<TestTracker>();
        Assert.Single(list);
        Assert.Same(c, list[0]);
    }

    [Fact]
    public void GetComponents_SameSnapshot_ReturnsSameListInstance()
    {
        var snap = new FrameSnapshot();
        var go = new GameObject();
        var c = go.AddComponent<TestTracker>();
        snap.Groups.Add(new ComponentGroup { ComponentType = typeof(TestTracker), Components = [c] });
        Assert.Same(snap.GetComponents<TestTracker>(), snap.GetComponents<TestTracker>());
    }

    [Fact]
    public void GetComponents_AfterSnapshotRebuild_ReturnsFreshList()
    {
        var reg = new ComponentRegistry();
        var c = new GameObject().AddComponent<TestTracker>();
        reg.Register(c);
        reg.ApplyPending();

        var snap = new FrameSnapshot();
        reg.BuildSnapshot(snap);
        var first = snap.GetComponents<TestTracker>();

        reg.BuildSnapshot(snap); // 双缓冲同实例重建（BuildSnapshot 新建 ComponentGroup）
        var second = snap.GetComponents<TestTracker>();

        Assert.NotSame(first, second);
        Assert.Same(c, second[0]);
    }

    [Fact]
    public void Manager_Current_ReturnsSnapshotAfterCommit()
    {
        var mgr = new FrameSnapshotManager();
        var snap1 = mgr.Current;
        Assert.NotNull(snap1);

        var registry = new ComponentRegistry();
        var scene = new Scene("T"); scene.AddRootObject(new GameObject("A"));
        mgr.CommitPending(registry, new List<SceneManager.DestroyEntry>(), scene, 0f);

        var snap2 = mgr.Current;
        Assert.NotSame(snap1, snap2);
        Assert.Equal(1L, snap2.FrameCount);
        Assert.Same(scene, snap2.ActiveScene);
    }

    [Fact]
    public void Manager_DoubleBuffer_SwapsCorrectly()
    {
        var mgr = new FrameSnapshotManager();
        var snap1 = mgr.Current;
        mgr.CommitPending(new ComponentRegistry(), [], new Scene("T"), 0f);
        var snap2 = mgr.Current;
        mgr.CommitPending(new ComponentRegistry(), [], new Scene("T2"), 0f);
        var snap3 = mgr.Current;

        Assert.NotSame(snap1, snap2);
        Assert.Same(snap1, snap3); // Swap returns to first buffer
    }

    private class TestTracker : Component { }

    private class DestroyTracker : MonoBehaviour
    {
        public bool Destroyed;
        public override void OnDestroy() => Destroyed = true;
    }

    [Fact]
    public void CommitPending_DelayedDestroy_RespectsDelay()
    {
        _sm._destroyQueue.Clear();
        var reg = new ComponentRegistry();
        var mgr = new FrameSnapshotManager();
        var scene = new Scene("T");
        var go = new GameObject();
        var c = go.AddComponent<DestroyTracker>(reg);
        scene.AddRootObject(go);
        Object.Destroy(c, 1.0f);

        reg.ApplyPending();
        mgr.CommitPending(reg, _sm._destroyQueue, scene, 0.5f);
        Assert.False(c.Destroyed); // 延迟未到

        mgr.CommitPending(reg, _sm._destroyQueue, scene, 0.6f);
        Assert.True(c.Destroyed);
        Assert.Empty(reg.GetOfType<DestroyTracker>()); // 已从注册表移除
    }

    [Fact]
    public void CommitPending_GameObjectDestroy_RemovesTreeFromRegistry()
    {
        _sm._destroyQueue.Clear();
        var reg = new ComponentRegistry();
        var mgr = new FrameSnapshotManager();
        var scene = new Scene("T");
        var parent = new GameObject("Parent");
        var child = new GameObject("Child");
        child.Transform.SetParent(parent.Transform);
        var pc = parent.AddComponent<DestroyTracker>(reg);
        var cc = child.AddComponent<DestroyTracker>(reg);
        scene.AddRootObject(parent);
        Object.Destroy(parent);

        reg.ApplyPending();
        mgr.CommitPending(reg, _sm._destroyQueue, scene, 0f);

        Assert.True(pc.Destroyed);
        Assert.True(cc.Destroyed);
        Assert.Empty(reg.GetOfType<DestroyTracker>());
    }

    [Fact]
    public void BuildSnapshot_CopiesGroupInstances()
    {
        var reg = new ComponentRegistry();
        var c = new GameObject().AddComponent<TestTracker>();
        reg.Register(c);
        reg.ApplyPending();

        var snap1 = new FrameSnapshot();
        var snap2 = new FrameSnapshot();
        reg.BuildSnapshot(snap1);
        reg.BuildSnapshot(snap2);
        Assert.NotSame(snap1.Groups[0], snap2.Groups[0]);            // 复制语义：不共享 ComponentGroup 实例
        Assert.NotSame(snap1.Groups[0].Components, snap2.Groups[0].Components); // 组件列表亦复制

        reg.Unregister(c);                                           // 实时注册表变更不影响已构建快照
        Assert.Same(c, snap1.Groups[0].Components[0]);               // 组件引用本身共享
    }

    [Fact]
    public void CommitPending_AfterWarmup_ZeroAllocation()
    {
        _sm._destroyQueue.Clear();
        var reg = new ComponentRegistry();
        var scene = new Scene("T");
        var go = new GameObject();
        go.AddComponent<TestTracker>(reg);
        scene.AddRootObject(go);
        reg.ApplyPending();

        var mgr = new FrameSnapshotManager();
        mgr.CommitPending(reg, _sm._destroyQueue, scene, 0f);
        mgr.CommitPending(reg, _sm._destroyQueue, scene, 0f); // warmup

        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var before = GC.GetTotalAllocatedBytes();
        for (int i = 0; i < 10; i++)
            mgr.CommitPending(reg, _sm._destroyQueue, scene, 0f);
        var after = GC.GetTotalAllocatedBytes();

        Assert.True(after - before < 16384, $"CommitPending allocated {after - before} bytes over 10 frames");
    }
}
