using SilkEngine.Assets;
using SilkEngine.Render;
using SilkEngine.Rendering.OpenGL;

namespace SilkEngine.Tests.Render;

public class RenderBackendStubTests
{
    private sealed class StubBackend : RenderBackendBase
    {
        public override bool ShouldClose => false;
        public override int Width => 1;
        public override int Height => 1;
        public override void InitWindow() { }
        public override void MakeContextCurrent() { }
        public override void ClearContext() { }
        public override void PumpWindowEvents() { }
        public override IRenderBuffer CreateBuffer(int sizeBytes) => new OpenGLBuffer(sizeBytes, () => { });
    }

    [Fact]
    public void ExecutePass_NotImplemented_Throws()
    {
        var backend = new StubBackend();
        Assert.Throws<NotSupportedException>(() => backend.ExecutePass([]));
    }

    [Fact]
    public void Present_NotImplemented_Throws()
    {
        var backend = new StubBackend();
        Assert.Throws<NotSupportedException>(() => backend.Present());
    }

    [Fact]
    public void DrawIndirect_NotImplemented_Throws()
    {
        var backend = new StubBackend();
        Assert.Throws<NotSupportedException>(() => backend.DrawIndirect(new OpenGLBuffer(1, () => { }), 0, 1));
    }

    [Fact]
    public void ReleaseTexture_NotImplemented_Throws()
    {
        var backend = new StubBackend();
        Assert.Throws<NotSupportedException>(() => backend.ReleaseTexture(new TextureAsset("t", new ImageData(1, 1, [1, 2, 3, 4]))));
    }
}
