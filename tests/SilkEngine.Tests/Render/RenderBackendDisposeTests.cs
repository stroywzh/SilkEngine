using SilkEngine.Render.OpenGL;

namespace SilkEngine.Tests.Render;

public class RenderBackendDisposeTests
{
    [Fact]
    public void Dispose_WithoutWindow_DoesNotThrow()
    {
        var backend = new OpenGLRenderBackend(); // 未 InitWindow：_window/_gl 为 null
        var ex = Record.Exception(backend.Dispose);
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_Twice_IsIdempotent()
    {
        var backend = new OpenGLRenderBackend();
        backend.Dispose();
        var ex = Record.Exception(backend.Dispose);
        Assert.Null(ex);
    }
}
