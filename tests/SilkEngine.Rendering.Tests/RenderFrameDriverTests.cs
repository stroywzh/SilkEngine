using SilkEngine.Rendering;
using Xunit;

namespace SilkEngine.Rendering.Tests;

public sealed class RenderFrameDriverTests
{
    [Fact]
    public void RenderFrame_TransfersRealRecordedCommandsToBackend()
    {
        using var backend = new RecordingBackend();
        backend.Initialize(new NullGraphicsDevice());
        using var pipeline = new TestPipeline
        {
            OnRecord = commands => commands.ClearColor(
                new RenderTargetHandle(42), System.Numerics.Vector4.One),
        };
        pipeline.Initialize(backend);
        var driver = new RenderFrameDriver(pipeline, backend);

        driver.RenderFrame(default);

        var command = Assert.Single(Assert.Single(backend.Frames));
        Assert.Equal((ushort)42, command.Target.Id);
        Assert.Equal(System.Numerics.Vector4.One, command.Color);
        Assert.Equal(0, backend.WaitForIdleCount);
    }

    [Fact]
    public void RenderFrame_RunsPipelineThenExecute_AndDoesNotIdle()
    {
        var calls = new List<string>();
        var pipeline = new TestPipeline { OnRecord = _ => calls.Add("record") };
        var backend = new SpyBackend { OnExecute = () => calls.Add("execute") };
        pipeline.Initialize(backend);

        var driver = new RenderFrameDriver(pipeline, backend);
        driver.RenderFrame(default);

        Assert.Equal(new[] { "record", "execute" }, calls);
        Assert.Equal(1, backend.ExecuteRenderCount);
        Assert.Equal(0, backend.WaitForIdleCount);
    }

    [Fact]
    public void RenderFrame_WhenPipelineThrows_DoesNotExecuteBackend()
    {
        var pipeline = new TestPipeline { ThrowOnRecord = true };
        var backend = new SpyBackend();
        pipeline.Initialize(backend);

        var driver = new RenderFrameDriver(pipeline, backend);

        Assert.Throws<InvalidOperationException>(() => driver.RenderFrame(default));
        Assert.Equal(0, backend.ExecuteRenderCount);
    }
}
