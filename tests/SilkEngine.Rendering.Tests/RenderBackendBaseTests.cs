using System.Numerics;
using SilkEngine.Rendering;
using SilkEngine.Rendering.Abstraction;
using Xunit;

namespace SilkEngine.Rendering.Tests;

public sealed class RenderBackendBaseTests
{
    private static RecordingBackend CreateBackend()
    {
        var backend = new RecordingBackend();
        backend.Initialize(new NullGraphicsDevice());
        return backend;
    }

    [Fact]
    public void Return_RecyclesButOldLeaseCannotBeReused()
    {
        using var backend = CreateBackend();
        var first = backend.RentCommandBuffer();
        first.ClearColor(new RenderTargetHandle(1), Vector4.One);

        backend.RecycleCommandBuffer(first);

        Assert.Throws<InvalidOperationException>(() =>
            first.ClearColor(new RenderTargetHandle(1), Vector4.Zero));
        Assert.Throws<InvalidOperationException>(() => { _ = first.CommandsCount; });
        Assert.Throws<InvalidOperationException>(() => backend.RecycleCommandBuffer(first));

        var second = backend.RentCommandBuffer();
        Assert.NotSame(first, second);
        Assert.Equal(0, second.CommandsCount);
        backend.RecycleCommandBuffer(second);
    }

    [Fact]
    public void Submit_TransfersOwnership_AndRejectsEmptyOrForeignBuffers()
    {
        using var backend = CreateBackend();
        using var other = CreateBackend();

        var empty = backend.RentCommandBuffer();
        Assert.Throws<ArgumentException>(() => backend.Submit(empty));
        empty.ClearColor(new RenderTargetHandle(2), Vector4.One);
        backend.RecycleCommandBuffer(empty);

        var foreign = other.RentCommandBuffer();
        foreign.ClearColor(new RenderTargetHandle(5), Vector4.One);
        Assert.Throws<ArgumentException>(() => backend.Submit(foreign));
        Assert.Throws<ArgumentException>(() => backend.Submit(new StubCommandBuffer()));
        foreign.ClearColor(new RenderTargetHandle(5), Vector4.Zero);
        other.RecycleCommandBuffer(foreign);

        var buffer = backend.RentCommandBuffer();
        buffer.ClearColor(new RenderTargetHandle(9), new Vector4(0, 1, 0, 1));
        backend.Submit(buffer);

        Assert.Throws<InvalidOperationException>(() =>
            buffer.ClearColor(new RenderTargetHandle(9), Vector4.Zero));
        Assert.Throws<InvalidOperationException>(() => backend.Submit(buffer));
        Assert.Throws<InvalidOperationException>(() => backend.RecycleCommandBuffer(buffer));

        backend.ExecuteRender();
        var command = Assert.Single(Assert.Single(backend.Frames));
        Assert.Equal(RenderCommandKind.ClearColor, command.Kind);
        Assert.Equal((ushort)9, command.Target.Id);
        Assert.Equal(new Vector4(0, 1, 0, 1), command.Color);
        Assert.Throws<InvalidOperationException>(() =>
            buffer.ClearColor(new RenderTargetHandle(9), Vector4.Zero));
    }

    [Fact]
    public void ExecuteRender_ConsumesOrderAndRecyclesCpuStorage()
    {
        using var backend = CreateBackend();
        var first = backend.RentCommandBuffer();
        first.ClearColor(new RenderTargetHandle(10), Vector4.One);
        backend.Submit(first);

        var second = backend.RentCommandBuffer();
        second.ClearColor(new RenderTargetHandle(11), Vector4.Zero);
        second.ClearColor(new RenderTargetHandle(12), Vector4.One);
        backend.Submit(second);

        backend.ExecuteRender();

        Assert.Equal(2, backend.Frames.Count);
        Assert.Equal((ushort)10, backend.Frames[0][0].Target.Id);
        Assert.Equal((ushort)11, backend.Frames[1][0].Target.Id);
        Assert.Equal((ushort)12, backend.Frames[1][1].Target.Id);

        var reused = backend.RentCommandBuffer();
        Assert.NotSame(first, reused);
        Assert.NotSame(second, reused);
        Assert.Equal(0, reused.CommandsCount);
        backend.RecycleCommandBuffer(reused);
    }

    [Fact]
    public void OperationsBeforeInitialize_Throw()
    {
        var backend = new RecordingBackend();
        Assert.Throws<InvalidOperationException>(() => backend.RentCommandBuffer());
        Assert.Throws<InvalidOperationException>(() => backend.ExecuteRender());
        Assert.Throws<InvalidOperationException>(() => backend.WaitForIdle());
        backend.Dispose();
    }

    [Fact]
    public void InitializeFailure_CleansOnceAndFaultsBackend()
    {
        var backend = new RecordingBackend { ThrowOnInitialize = true };

        Assert.Throws<InvalidOperationException>(() =>
            backend.Initialize(new NullGraphicsDevice()));

        Assert.Equal(1, backend.DisposeCount);
        Assert.Throws<InvalidOperationException>(() =>
            backend.Initialize(new NullGraphicsDevice()));
        Assert.Throws<InvalidOperationException>(() => backend.RentCommandBuffer());

        backend.Dispose();
        Assert.Equal(1, backend.DisposeCount);
    }

    [Fact]
    public void ExecuteFailure_DropsQueuedFrames_AndKeepsBackendUsable()
    {
        using var backend = CreateBackend();
        var first = backend.RentCommandBuffer();
        first.ClearColor(new RenderTargetHandle(20), Vector4.One);
        backend.Submit(first);

        var second = backend.RentCommandBuffer();
        second.ClearColor(new RenderTargetHandle(21), Vector4.Zero);
        backend.Submit(second);

        backend.ThrowOnExecuteCall = 1;

        var error = Assert.Throws<InvalidOperationException>(() => backend.ExecuteRender());
        Assert.Equal("execute failed", error.Message);
        Assert.Empty(backend.Frames); // 没有任何一帧被重放

        Assert.Throws<InvalidOperationException>(() =>
            first.ClearColor(new RenderTargetHandle(20), Vector4.Zero));
        Assert.Throws<InvalidOperationException>(() => { _ = second.CommandsCount; });

        var third = backend.RentCommandBuffer();
        third.ClearColor(new RenderTargetHandle(22), Vector4.One);
        backend.Submit(third);
        backend.ExecuteRender();

        var frame = Assert.Single(backend.Frames);
        Assert.Equal((ushort)22, Assert.Single(frame).Target.Id);
    }

    [Fact]
    public void PresentFailure_KeepsBackendUsable()
    {
        using var backend = CreateBackend();
        backend.ThrowOnPresent = true;
        var first = backend.RentCommandBuffer();
        first.ClearColor(new RenderTargetHandle(40), Vector4.One);
        backend.Submit(first);

        Assert.Throws<InvalidOperationException>(() => backend.ExecuteRender());
        Assert.Single(backend.Frames); // 命令已消费，只有呈现失败

        backend.ThrowOnPresent = false;
        var second = backend.RentCommandBuffer();
        second.ClearColor(new RenderTargetHandle(41), Vector4.One);
        backend.Submit(second);
        backend.ExecuteRender();

        Assert.Equal(2, backend.Frames.Count);
    }

    [Fact]
    public void WaitForIdleFailure_KeepsBackendUsable()
    {
        using var backend = CreateBackend();
        backend.ThrowOnWaitForIdle = true;
        Assert.Throws<InvalidOperationException>(() => backend.WaitForIdle());

        backend.ThrowOnWaitForIdle = false;
        backend.WaitForIdle();
        Assert.Equal(1, backend.WaitForIdleCount);
    }

    [Fact]
    public void Dispose_IsIdempotent_AndInvalidatesLiveLeases()
    {
        var backend = CreateBackend();
        var buffer = backend.RentCommandBuffer();

        backend.Dispose();
        backend.Dispose();

        Assert.Equal(1, backend.DisposeCount);
        Assert.Throws<ObjectDisposedException>(() =>
            buffer.ClearColor(new RenderTargetHandle(1), Vector4.One));
        Assert.Throws<ObjectDisposedException>(() => backend.RentCommandBuffer());
        Assert.Throws<ObjectDisposedException>(() => backend.ExecuteRender());
    }

    [Fact]
    public void WaitForIdle_IsNotCalledByExecuteRender()
    {
        using var backend = CreateBackend();
        backend.ExecuteRender();
        Assert.Equal(0, backend.WaitForIdleCount);
        backend.WaitForIdle();
        Assert.Equal(1, backend.WaitForIdleCount);
    }

    [Fact]
    public void ExecuteCallback_RejectsReentryWithoutCorruptingQueue()
    {
        using var backend = CreateBackend();
        var buffer = backend.RentCommandBuffer();
        buffer.ClearColor(new RenderTargetHandle(30), Vector4.One);
        backend.Submit(buffer);
        backend.OnExecute = () =>
        {
            Assert.Throws<InvalidOperationException>(() => backend.ExecuteRender());
            Assert.Throws<InvalidOperationException>(() => backend.RentCommandBuffer());
            Assert.Throws<InvalidOperationException>(() => backend.Dispose());
        };

        backend.ExecuteRender();

        Assert.Single(backend.Frames);
    }
}
