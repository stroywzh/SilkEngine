using SilkEngine.Math;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.Backend;

namespace SilkEngine.Tests.Rendering;

public class BackendContractTests
{
    [Fact]
    public void BackendContract_ExecutesPacketsAndReleasesRenderHandles()
    {
        IRenderBackend backend = new RecordingBackend();
        var packet = new RenderPacket(
            new RenderShaderHandle(1),
            new RenderMeshHandle(2),
            new RenderTextureHandle(3),
            new RenderMaterialParameters([("Roughness", RenderParameterValue.Float(0.5f))]),
            Matrix4x4.Identity);

        backend.Execute(packet);
        backend.Release(new RenderResourceReleaseRequest(RenderResourceKind.Texture, 4));

        var recording = Assert.IsType<RecordingBackend>(backend);
        Assert.Same(packet, recording.LastPacket);
        Assert.Equal(4UL, recording.ReleasedHandle);
    }

    private sealed class RecordingBackend : IRenderBackend
    {
        public RenderPacket? LastPacket { get; private set; }

        public ulong ReleasedHandle { get; private set; }

        public void Initialize()
        {
        }

        public void Execute(RenderPacket packet) => LastPacket = packet;

        public void Present()
        {
        }

        public void Release(RenderResourceReleaseRequest request) => ReleasedHandle = request.Handle;

        public void Dispose()
        {
        }
    }
}
