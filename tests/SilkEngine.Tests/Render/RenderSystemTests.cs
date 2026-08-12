using SilkEngine;
using SilkEngine.Render;

namespace SilkEngine.Tests.Render;
using Scene = SilkEngine.Scene;

public class RenderCollectorTests
{
    [Fact]
    public void Gather_NoCamera_ReturnsDefaultCamera()
    {
        var collector = new RenderCollector();
        var snap = new FrameSnapshot();
        snap.ActiveScene = new Scene("T");

        collector.Gather(snap, out var camera, out var batches);
        Assert.NotNull(camera);
        Assert.Empty(batches);
    }

    [Fact]
    public void Gather_UsesSceneCamera()
    {
        var scene = new Scene("T");
        var camObj = new GameObject("Cam");
        var cam = camObj.AddComponent<Camera>();
        var go = new GameObject();
        var mr = go.AddComponent<MeshRenderer>();
        mr.Enabled = true; go.IsActive = true;
        scene.AddRootObject(camObj);
        scene.AddRootObject(go);

        var reg = new ComponentRegistry();
        reg.Register(cam);
        reg.Register(mr);
        reg.ApplyPending();

        var snap = new FrameSnapshot();
        snap.ActiveScene = scene;
        reg.RefreshSnapshot(snap);

        var collector = new RenderCollector();
        collector.Gather(snap, out var foundCam, out var batches);
        Assert.Same(cam, foundCam);
        Assert.NotEmpty(batches);
    }
}
