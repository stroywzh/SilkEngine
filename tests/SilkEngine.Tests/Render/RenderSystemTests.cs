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

public class ForwardPipelineTests
{
    [Fact]
    public void ForwardPipeline_Build_CreatesSinglePass()
    {
        var pipeline = new ForwardPipeline();
        var camObj = new GameObject();
        var cam = camObj.AddComponent<Camera>();
        cam.Orthographic = true;
        cam.UpdateMatrices(800f / 600f);

        var mr = new GameObject().AddComponent<MeshRenderer>();
        mr.Mesh = new Mesh { Name = "Test", Layout = [] };
        mr.Shader = new Shader { Name = "S" };
        mr.Material = new Material();
        var batches = new List<RenderBatch> { new() { Camera = cam, Renderers = [mr] } };

        var passes = pipeline.Build(cam, batches);
        Assert.Single(passes);
        Assert.NotEmpty(passes[0].Commands);
        Assert.IsType<SingleDrawCommand>(passes[0].Commands[0]);
    }
}

public class FakeRenderBackend : IRenderBackend
{
    public List<IReadOnlyList<DrawCommand>> Passes = [];
    public int PresentCount;
    public bool ShouldCloseVal;
    public bool ShouldClose => ShouldCloseVal;
    public int Width => 800;
    public int Height => 600;
    public Silk.NET.Windowing.IWindow? NativeWindow => null;
    public void InitWindow() { }
    public void MakeContextCurrent() { }
    public void ClearContext() { }
    public void PumpWindowEvents() { }
    public void ExecuteFrame(IReadOnlyList<DrawCommand> commands) { }
    public void ExecutePass(IReadOnlyList<DrawCommand> commands) => Passes.Add(commands);
    public void Present() => PresentCount++;
    public IntPtr CreateBuffer(int size) => IntPtr.Zero;
    public void DrawIndirect(IntPtr buf, int off, int cnt) { }
    public void Dispose() { }
}

public class RenderSystemTests
{
    [Fact]
    public void RenderSystem_Render_CallsBackendPresent()
    {
        using var backend = new FakeRenderBackend();
        var sys = new RenderSystem(backend);

        var snap = new FrameSnapshot();
        var scene = new Scene("T");
        var go = new GameObject();
        var mr = go.AddComponent<MeshRenderer>();
        mr.Enabled = true; go.IsActive = true;
        mr.Shader = new Shader { Name = "S" };
        mr.Mesh = new Mesh { Name = "M", Layout = [] };
        scene.AddRootObject(go);

        var camObj = new GameObject("Cam");
        var cam = camObj.AddComponent<Camera>();
        scene.AddRootObject(camObj);

        var reg = new ComponentRegistry();
        reg.Register(mr); reg.Register(cam);
        reg.ApplyPending();
        reg.RefreshSnapshot(snap);
        snap.ActiveScene = scene;

        sys.Render(snap);
        Assert.Equal(1, backend.PresentCount);
        Assert.Single(backend.Passes);
        Assert.NotEmpty(backend.Passes[0]);
    }
}
