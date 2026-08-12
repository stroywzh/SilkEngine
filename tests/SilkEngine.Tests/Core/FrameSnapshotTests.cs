using System.Collections.Generic;
using SilkEngine;

namespace SilkEngine.Tests.Core;
using Scene = SilkEngine.Scene;

public class FrameSnapshotTests
{
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
    public void Manager_Current_ReturnsSnapshotAfterCommit()
    {
        var mgr = new FrameSnapshotManager();
        var snap1 = mgr.Current;
        Assert.NotNull(snap1);

        var registry = new ComponentRegistry();
        var scene = new Scene("T"); scene.AddRootObject(new GameObject("A"));
        mgr.CommitPending(registry, new List<SceneManager.DestroyEntry>(), scene);

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
        mgr.CommitPending(new ComponentRegistry(), [], new Scene("T"));
        var snap2 = mgr.Current;
        mgr.CommitPending(new ComponentRegistry(), [], new Scene("T2"));
        var snap3 = mgr.Current;

        Assert.NotSame(snap1, snap2);
        Assert.Same(snap1, snap3); // Swap returns to first buffer
    }

    private class TestTracker : Component { }
}
