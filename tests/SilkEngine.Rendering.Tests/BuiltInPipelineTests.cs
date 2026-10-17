using SilkEngine.Rendering;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.BuiltIn;
using SilkEngine.Rendering.BuiltIn.Backend;
using Xunit;

namespace SilkEngine.Rendering.Tests;

public sealed class BuiltInPipelineTests
{
    [Fact]
    public void FrameDelta_ChangesColor_AndUsesExplicitWindowTarget()
    {
        using var backend = new RecordingBackend();
        backend.Initialize(new NullGraphicsDevice());
        using var pipeline = new BuiltInPipeline();
        pipeline.Initialize(backend);
        pipeline.RenderFrame(new RenderContext(1, 0));
        backend.ExecuteRender();
        pipeline.RenderFrame(new RenderContext(2, 0.5));
        backend.ExecuteRender();
        Assert.Equal(2, backend.Frames.Count);
        var first = Assert.Single(backend.Frames[0]);
        var second = Assert.Single(backend.Frames[1]);
        Assert.Equal(RenderCommandKind.ClearColor, first.Kind);
        Assert.Equal((ushort)1, first.Target.Id);
        Assert.Equal(first.Target.Id, second.Target.Id);
        Assert.NotEqual(first.Color, second.Color);
        Assert.InRange(second.Color.X, 0f, 1f);
        Assert.InRange(second.Color.Y, 0f, 1f);
        Assert.InRange(second.Color.Z, 0f, 1f);
        Assert.Equal(1f, second.Color.W);
    }

    [Fact]
    public void SilkBackend_AdvertisesWindowHostWithoutOpeningAWindow()
    {
        Assert.True(typeof(IRenderWindowHost).IsAssignableFrom(typeof(SilkOpenGLBackend)));
    }
}
