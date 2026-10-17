using System.Numerics;
using SilkEngine.Rendering;
using SilkEngine.Rendering.Abstraction;
using Xunit;

namespace SilkEngine.Rendering.Tests;

public sealed class RenderPipelineBaseTests
{
    private static TestPipeline CreatePipeline(out SpyBackend backend)
    {
        backend = new SpyBackend();
        var pipeline = new TestPipeline();
        pipeline.Initialize(backend);
        return pipeline;
    }

    [Fact]
    public void RenderFrame_WithCommands_SubmitsWithoutReturning()
    {
        var pipeline = CreatePipeline(out var backend);
        pipeline.OnRecord = commands =>
            commands.ClearColor(new RenderTargetHandle(3), new Vector4(1, 0, 0, 1));

        pipeline.RenderFrame(default);

        Assert.Equal(1, backend.RentCount);
        Assert.Equal(1, backend.SubmitCount);
        Assert.Equal(0, backend.ReturnCount);
        Assert.False(backend.LastRented!.IsWritable);
    }

    [Fact]
    public void RenderFrame_WithoutCommands_ReturnsWithoutSubmitting()
    {
        var pipeline = CreatePipeline(out var backend);

        pipeline.RenderFrame(default);

        Assert.Equal(1, backend.ReturnCount);
        Assert.Equal(0, backend.SubmitCount);
    }

    [Fact]
    public void RenderFrame_WhenRecordingThrows_ReturnsAndRethrowsOriginal()
    {
        var pipeline = CreatePipeline(out var backend);
        pipeline.ThrowOnRecord = true;

        var ex = Assert.Throws<InvalidOperationException>(() => pipeline.RenderFrame(default));

        Assert.Equal("record failed", ex.Message);
        Assert.Equal(1, backend.ReturnCount);
        Assert.Equal(0, backend.SubmitCount);
    }

    [Fact]
    public void RenderFrame_WhenSubmitThrows_ReturnsAndRethrowsOriginal()
    {
        var pipeline = CreatePipeline(out var backend);
        pipeline.OnRecord = commands =>
            commands.ClearColor(new RenderTargetHandle(4), Vector4.One);
        backend.ThrowOnSubmit = true;

        var ex = Assert.Throws<InvalidOperationException>(() => pipeline.RenderFrame(default));

        Assert.Equal("submit failed", ex.Message);
        Assert.Equal(1, backend.ReturnCount);
        Assert.False(backend.LastRented!.IsWritable);
    }

    [Fact]
    public void RenderFrame_BeforeInitialize_Throws()
    {
        var pipeline = new TestPipeline();
        Assert.Throws<InvalidOperationException>(() => pipeline.RenderFrame(default));
    }

    [Fact]
    public void Initialize_WhenOnInitializeThrows_DisposesCoreOnceAndFaults()
    {
        var pipeline = new TestPipeline { ThrowOnInitialize = true };
        var backend = new SpyBackend();

        Assert.Throws<InvalidOperationException>(() => pipeline.Initialize(backend));

        Assert.Equal(1, pipeline.DisposeCount);
        Assert.Throws<InvalidOperationException>(() => pipeline.Initialize(backend));
        pipeline.Dispose();
        Assert.Equal(1, pipeline.DisposeCount);
    }

    [Fact]
    public void Dispose_IsIdempotent_AndBlocksFurtherFrames()
    {
        var pipeline = CreatePipeline(out _);

        pipeline.Dispose();
        pipeline.Dispose();

        Assert.Equal(1, pipeline.DisposeCount);
        Assert.Throws<ObjectDisposedException>(() => pipeline.RenderFrame(default));
        Assert.Throws<ObjectDisposedException>(() => pipeline.Resize(1, 1));
    }

    [Fact]
    public void Recording_RejectsLifecycleReentry_AndLeavesBackendOwnedByHost()
    {
        var pipeline = CreatePipeline(out var backend);
        pipeline.OnRecord = _ =>
        {
            Assert.Throws<InvalidOperationException>(() => pipeline.RenderFrame(default));
            Assert.Throws<InvalidOperationException>(() => pipeline.Resize(1, 1));
            Assert.Throws<InvalidOperationException>(() => pipeline.Dispose());
        };

        pipeline.RenderFrame(default);
        pipeline.Dispose();

        Assert.Equal(1, backend.ReturnCount);
        Assert.False(backend.Disposed);
    }

    [Fact]
    public void Resize_RejectsNegativeAndSkipsZeroDimensions()
    {
        using var pipeline = CreatePipeline(out _);
        Assert.Throws<ArgumentOutOfRangeException>(() => pipeline.Resize(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => pipeline.Resize(1, -1));
        pipeline.Resize(0, 100);
        Assert.Equal(0, pipeline.ResizeCount);
        pipeline.Resize(640, 480);
        Assert.Equal((640, 480), pipeline.LastResize);
    }

    [Fact]
    public void RecordingFailure_PropagatesOriginal_AndAllowsNextFrame()
    {
        using var pipeline = CreatePipeline(out var backend);
        pipeline.ThrowOnRecord = true;
        Assert.Throws<InvalidOperationException>(() => pipeline.RenderFrame(default));

        pipeline.ThrowOnRecord = false;
        pipeline.OnRecord = commands =>
            commands.ClearColor(new RenderTargetHandle(31), Vector4.One);
        pipeline.RenderFrame(default);

        Assert.Equal(2, backend.RentCount);
        Assert.Equal(1, backend.ReturnCount);
        Assert.Equal(1, backend.SubmitCount);
        Assert.False(backend.LastRented!.IsWritable);
    }

    [Fact]
    public void RenderFrameFromAnotherThread_Throws()
    {
        using var backend = new SpyBackend();
        using var pipeline = new TestPipeline();
        pipeline.Initialize(backend);

        Exception? captured = null;
        // A real thread is used because a pool task can be inlined onto the calling thread.
        var thread = new Thread(() =>
        {
            try
            {
                pipeline.RenderFrame(default);
            }
            catch (Exception error)
            {
                captured = error;
            }
        });
        thread.Start();
        thread.Join();

        var invalid = Assert.IsType<InvalidOperationException>(captured);
        Assert.Contains("owned by thread", invalid.Message, StringComparison.OrdinalIgnoreCase);
    }
}
