using SilkEngine.Core.Assets;
using SilkEngine.Render;

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
        Assert.Throws<NotSupportedException>(() => backend.DrawIndirect(IntPtr.Zero, 0, 1));
    }

    [Fact]
    public void ReleaseTexture_NotImplemented_Throws()
    {
        var backend = new StubBackend();
        Assert.Throws<NotSupportedException>(() => backend.ReleaseTexture(new Texture2D()));
    }
}
