using SilkEngine.Core;
using SilkEngine.Math;
using SilkEngine.Rendering;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.Backend;
using SilkEngine.Rendering.Pipeline;
using SilkEngine.Scene;
using SilkEngine.Threading;

namespace SilkEngine.Tests.Rendering;

/// <summary>
/// CameraBlock 契约：RenderSystem 在 Main 域把 ICameraView 的 View/Projection 矩阵
/// 复制进不可变 RenderSubmission（随提交下发，不突变共享材质）。
/// 与 Assets 集合串行：RenderSystem ctor 自注册全局 Services。
/// </summary>
[Collection("Assets")]
public class CameraBlockTests
{
    private sealed class CapturingBackend : IRenderBackend, IRenderFrameExecutor
    {
        public RenderSubmission? LastSubmission;
        private ulong _nextHandle = 1;

        public void Initialize() { }

        public void Execute(RenderPacket packet) { }

        public void ExecuteFrame(RenderSubmission submission) => LastSubmission = submission;

        public void Present() { }

        public void Release(RenderResourceReleaseRequest request) { }

        public RenderTextureHandle CreateTexture(RenderTextureCreateRequest request) => new(_nextHandle++);

        public RenderShaderHandle CreateShader(RenderShaderCreateRequest request) => new(_nextHandle++);

        public RenderMeshHandle CreateMesh(RenderMeshCreateRequest request) => new(_nextHandle++);

        public void Dispose() { }
    }

    private sealed class StubRenderable : IRenderable
    {
        public RenderShaderHandle ShaderHandle => new(1);
        public RenderMeshHandle MeshHandle => new(2);
        public RenderTextureHandle TextureHandle => default;
        public RenderMaterialParameters MaterialParameters => new([]);
        public bool Enabled => true;
        public Matrix4x4 WorldMatrix => Matrix4x4.Identity;
    }

    [Fact]
    public void RenderSystem_CopiesCameraMatricesIntoSubmission()
    {
        using var runtime = new ThreadRuntime();
        runtime.RegisterMainThread();
        var backend = new CapturingBackend();
        using var sys = new RenderSystem(backend, runtime);
        sys.Initialize();
        try
        {
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.Orthographic = true;
            cam.OrthographicSize = 4f;
            var batches = new List<RenderBatch> { new() { Renderers = [new StubRenderable()] } };

            sys.Render(16f / 9f, cam, batches);

            var submission = backend.LastSubmission;
            Assert.NotNull(submission);
            Assert.Equal(cam.ViewMatrix, submission!.Camera.View);
            Assert.Equal(cam.ProjectionMatrix, submission.Camera.Projection);
            Assert.Single(submission.Packets);
            Assert.Equal(RenderResourceCreateBatch.Empty, submission.Creates);
        }
        finally
        {
            Services.Unregister<RenderSystem>();
        }
    }

    [Fact]
    public void RenderSystem_NullCamera_SubmitsNothing()
    {
        using var runtime = new ThreadRuntime();
        runtime.RegisterMainThread();
        var backend = new CapturingBackend();
        using var sys = new RenderSystem(backend, runtime);
        sys.Initialize();
        try
        {
            sys.Render(1f, null, []);
            Assert.Null(backend.LastSubmission);
        }
        finally
        {
            Services.Unregister<RenderSystem>();
        }
    }
}
