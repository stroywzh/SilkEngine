using SilkEngine.Render.OpenGL;

namespace SilkEngine.Tests.Render;

public class RenderBufferContractTests
{
    [Fact]
    public void OpenGLBuffer_Dispose_InvokesDeleteOnce_AndIsIdempotent()
    {
        var deletes = 0;
        var buffer = new OpenGLBuffer(64, () => deletes++);
        Assert.False(buffer.IsDisposed);
        buffer.Dispose();
        Assert.True(buffer.IsDisposed);
        buffer.Dispose(); // 幂等
        Assert.Equal(1, deletes);
    }

    [Fact]
    public void OpenGLBuffer_Disposed_ThrowsOnUse()
    {
        var buffer = new OpenGLBuffer(64, () => { });
        buffer.Dispose();
        Assert.Throws<ObjectDisposedException>(() => buffer.ThrowIfDisposed());
    }

    [Fact]
    public void OpenGLBuffer_ExposesSize()
    {
        var buffer = new OpenGLBuffer(128, () => { });
        Assert.Equal(128, buffer.SizeBytes);
    }
}
