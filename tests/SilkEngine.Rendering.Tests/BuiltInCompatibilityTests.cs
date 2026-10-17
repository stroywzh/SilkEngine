using SilkEngine.Rendering;
using SilkEngine.Rendering.BuiltIn;
using SilkEngine.Rendering.BuiltIn.Backend;
using Xunit;

namespace SilkEngine.Rendering.Tests;

public sealed class BuiltInCompatibilityTests
{
    [Fact]
    public void BuiltInPipeline_EmptyFrame_DoesNotSubmitAndExecutesBackend()
    {
        using var backend = new RecordingBackend();
        backend.Initialize(new NullGraphicsDevice());
        using var pipeline = new BuiltInPipeline();
        pipeline.Initialize(backend);

        var driver = new RenderFrameDriver(pipeline, backend);
        driver.RenderFrame(default);

        Assert.Empty(backend.Frames);
        Assert.Equal(0, backend.WaitForIdleCount);
    }

    [Fact]
    public void SilkOpenGLBackend_Initialize_IsExplicitlyNotSupported()
    {
        using var backend = new SilkOpenGLBackend();
        Assert.Throws<NotSupportedException>(() => backend.Initialize(new NullGraphicsDevice()));
    }
}
