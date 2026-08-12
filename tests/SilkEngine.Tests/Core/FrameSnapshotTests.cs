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

    private class TestTracker : Component { }
}
