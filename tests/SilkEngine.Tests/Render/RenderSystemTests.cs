using SilkEngine.Core;
using SilkEngine.Assets;
using SilkEngine.Math;
using SilkEngine.Render;
using SilkEngine.Scene;
using SilkEngine.Threading;

namespace SilkEngine.Tests.Render;

public class RenderInterfaceContractTests
{
    [Fact]
    public void MeshRenderer_SatisfiesIRenderableContract()
    {
        var mr = new GameObject("MR").AddComponent<MeshRenderer>();
        mr.Shader = new Shader { Name = "S" };
        mr.Mesh = new Mesh { Name = "M", Layout = [] };
        var material = new Material(new MaterialReference(new AssetId(Guid.NewGuid())));
        mr.Material = material;

        IRenderable r = mr;
        Assert.Same(mr.Shader, r.Shader);
        Assert.Same(mr.Mesh, r.Mesh);
        Assert.Same(mr.Material, r.Material);
        Assert.Equal(mr.Enabled, r.Enabled);
        Assert.Equal(mr.Transform.LocalToWorldMatrix, r.WorldMatrix);
    }

    [Fact]
    public void Camera_SatisfiesICameraViewContract()
    {
        var cam = new GameObject("Cam").AddComponent<Camera>();
        cam.Orthographic = true;
        cam.Transform.LocalPosition = new Vector3(0, 0, -5);

        ICameraView view = cam;
        view.UpdateMatrices(800f / 600f);

        Assert.Equal(cam.ViewMatrix, view.ViewMatrix);
        Assert.Equal(cam.ProjectionMatrix, view.ProjectionMatrix);
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
        mr.Material = new Material(new MaterialReference(new AssetId(Guid.NewGuid())));
        var batches = new List<RenderBatch> { new() { Renderers = [mr] } };

        var passes = pipeline.Build(cam, batches);
        Assert.Single(passes);
        Assert.NotEmpty(passes[0].Commands);
        Assert.IsType<SingleDrawCommand>(passes[0].Commands[0]);
    }

    [Fact]
    public void ForwardPipeline_Build_CommandsCarryCameraMatrices()
    {
        var pipeline = new ForwardPipeline();
        var camGo = new GameObject();
        var cam = camGo.AddComponent<Camera>();
        cam.Orthographic = true;
        camGo.Transform.LocalPosition = new Vector3(0, 0, -5);
        cam.UpdateMatrices(800f / 600f);

        var mr = new GameObject().AddComponent<MeshRenderer>();
        mr.Mesh = new Mesh { Name = "Test", Layout = [] };
        mr.Shader = new Shader { Name = "S" };
        var batches = new List<RenderBatch> { new() { Renderers = [mr] } };

        var passes = pipeline.Build(cam, batches);
        var cmd = Assert.IsType<SingleDrawCommand>(passes[0].Commands[0]);
        Assert.NotNull(cmd.ViewMatrix);
        Assert.NotNull(cmd.ProjectionMatrix);
        Assert.Equal(cam.ViewMatrix.M11, cmd.ViewMatrix!.Value.M11);
        Assert.Equal(cam.ProjectionMatrix.M11, cmd.ProjectionMatrix!.Value.M11);
    }

    [Fact]
    public void ForwardPipeline_Build_UnresolvableMaterial_CommandMaterialIsNull()
    {
        var pipeline = new ForwardPipeline();
        var camGo = new GameObject();
        var cam = camGo.AddComponent<Camera>();
        camGo.Transform.LocalPosition = new Vector3(0, 0, -5);
        cam.UpdateMatrices(1f);

        var mr = new GameObject().AddComponent<MeshRenderer>();
        mr.Mesh = new Mesh { Name = "Test", Layout = [] };
        mr.Shader = new Shader { Name = "S" };
        mr.Material = new Material(new MaterialReference(new AssetId(Guid.NewGuid())));
        var batches = new List<RenderBatch> { new() { Renderers = [mr] } };

        var passes = pipeline.Build(cam, batches);
        var cmd = Assert.IsType<SingleDrawCommand>(passes[0].Commands[0]);
        Assert.Null(cmd.Material);   // 默认绑定无资产解析器 → Loading → 命令不带材质载荷
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
    public void ExecutePass(IReadOnlyList<DrawCommand> commands) => Passes.Add(commands);
    public void Present() => PresentCount++;
    public IRenderBuffer CreateBuffer(int sizeBytes) => new StubBuffer();
    public void DrawIndirect(IRenderBuffer buffer, int offset, int drawCount) { }
    public void ReleaseTexture(TextureAsset texture) { }
    public void ReleaseGpuResource(IAsset asset) { }
    public void Dispose() { }

    private sealed class StubBuffer : IRenderBuffer
    {
        public int SizeBytes => 0;
        public bool IsDisposed => false;
        public void Dispose() { }
    }
}

public class RenderSystemTests
{
    [Fact]
    public void RenderSystem_Render_CallsBackendPresent()
    {
        using var backend = new FakeRenderBackend();
        using var sys = new RenderSystem(backend, new ThreadManager());
        sys.Initialize();
        try
        {
            var mr = new GameObject("MR").AddComponent<MeshRenderer>();
            mr.Shader = new Shader { Name = "S" };
            mr.Mesh = new Mesh { Name = "M", Layout = [] };
            var cam = new GameObject("Cam").AddComponent<Camera>();
            var batches = new List<RenderBatch> { new() { Renderers = [mr] } };

            sys.Render(800f / 600f, cam, batches);
            Assert.Equal(1, backend.PresentCount);
            Assert.Single(backend.Passes);
            Assert.NotEmpty(backend.Passes[0]);
        }
        finally
        {
            Services.Unregister<RenderSystem>();
        }
    }

    [Fact]
    public void RenderSystem_Render_NullCamera_SubmitsNothing()
    {
        using var backend = new FakeRenderBackend();
        using var sys = new RenderSystem(backend, new ThreadManager());
        sys.Initialize();
        try
        {
            sys.Render(1f, null, []);
            Assert.Equal(0, backend.PresentCount);
            Assert.Empty(backend.Passes);
        }
        finally
        {
            Services.Unregister<RenderSystem>();
        }
    }
}
