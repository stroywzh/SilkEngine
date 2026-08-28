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
/// 新 RenderSystem 契约测试：ThreadRuntime 托管 + RenderThreadHost 帧同步，
/// 管线构建 RenderPacket 列表并提交（Present 每帧一次）。
/// </summary>
public class RenderSystemTests
{
    private sealed class RecordingBackend : IRenderBackend
    {
        public List<RenderPacket> Packets = [];
        public int PresentCount;

        public void Initialize() { }

        public void Execute(RenderPacket packet) => Packets.Add(packet);

        public void Present() => PresentCount++;

        public void Release(RenderResourceReleaseRequest request) { }

        public RenderTextureHandle CreateTexture(RenderTextureCreateRequest request) => new(1);

        public RenderShaderHandle CreateShader(RenderShaderCreateRequest request) => new(1);

        public RenderMeshHandle CreateMesh(RenderMeshCreateRequest request) => new(1);

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
    public void RenderSystem_Render_BuildsAndSubmitsPackets()
    {
        using var backend = new RecordingBackend();
        using var runtime = new ThreadRuntime();
        runtime.RegisterMainThread();
        using var sys = new RenderSystem(backend, runtime);
        sys.Initialize();
        try
        {
            var cam = new GameObject("Cam").AddComponent<Camera>();
            var batches = new List<RenderBatch> { new() { Renderers = [new StubRenderable()] } };

            sys.Render(800f / 600f, cam, batches);

            var packet = Assert.Single(backend.Packets);
            Assert.Equal(2UL, packet.Mesh.Value);
            Assert.Equal(1UL, packet.Shader.Value);
            Assert.Equal(1, backend.PresentCount);
        }
        finally
        {
            Services.Unregister<RenderSystem>();
        }
    }

    [Fact]
    public void RenderSystem_Render_NullCamera_SubmitsNothing()
    {
        using var backend = new RecordingBackend();
        using var runtime = new ThreadRuntime();
        runtime.RegisterMainThread();
        using var sys = new RenderSystem(backend, runtime);
        sys.Initialize();
        try
        {
            sys.Render(1f, null, []);
            Assert.Equal(0, backend.PresentCount);
            Assert.Empty(backend.Packets);
        }
        finally
        {
            Services.Unregister<RenderSystem>();
        }
    }
}
