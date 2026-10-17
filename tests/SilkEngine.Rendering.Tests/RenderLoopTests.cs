using SilkEngine.Rendering;
using Xunit;

namespace SilkEngine.Rendering.Tests;

[Collection(EngineHostCollection.Name)]
public sealed class RenderLoopTests
{
    [Fact]
    public void Completion_WaitsForOutput_AndRejectsOverwrite()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var backend = new LoopBackend { SkipAttempts = 3 };
        backend.BeforeOutput = () =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException();
        };
        var pipeline = new LoopPipeline();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var loop = new LoopHarness(pipeline, backend);
        try
        {
            loop.Ready(timeout.Token);
            loop.Publish(1);
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            Assert.Throws<InvalidOperationException>(() => loop.Publish(2));
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            Assert.ThrowsAny<OperationCanceledException>(() => loop.Wait(canceled.Token));
            release.Set();
            loop.Wait(timeout.Token);
            Assert.Equal(1, backend.Executes);
            Assert.True(backend.Outputs >= 4);
            Assert.True(backend.Pumps > 0);
            loop.Publish(2);
            loop.Wait(timeout.Token);
        }
        finally
        {
            release.Set();
            loop.Dispose();
        }
        Assert.Equal(backend.OwnerThread, backend.DisposeThread);
    }

    [Fact]
    public void InitializationFailure_IsObservedByReadyWaiter()
    {
        var error = new InvalidOperationException("init failed");
        var backend = new LoopBackend { InitializeError = error };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var loop = new LoopHarness(new LoopPipeline(), backend);
        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => loop.Ready(timeout.Token)));
    }

    [Fact]
    public void RecordingFailure_PropagatesOriginal_AndDoesNotExecuteBackend()
    {
        var error = new InvalidOperationException("record failed");
        var backend = new LoopBackend();
        var pipeline = new LoopPipeline { Record = _ => throw error };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var loop = new LoopHarness(pipeline, backend);
        loop.Ready(timeout.Token);
        loop.Publish(1);
        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => loop.Wait(timeout.Token)));
        Assert.Equal(0, backend.Executes);
        Assert.Throws<InvalidOperationException>(() => loop.Publish(2));
    }

    [Fact]
    public void Stop_RejectsNewFrames_AndReadyDoesNotHang()
    {
        using var loop = new LoopHarness(new LoopPipeline(), new LoopBackend());
        loop.Stop();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Assert.ThrowsAny<OperationCanceledException>(() => loop.Ready(timeout.Token));
        Assert.ThrowsAny<OperationCanceledException>(() => loop.Publish(1));
    }

    [Fact]
    public void OutputFailure_IsNotPublishedAsSuccessfulFrame()
    {
        var error = new InvalidOperationException("swap failed");
        var backend = new LoopBackend { BeforeOutput = () => throw error };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var loop = new LoopHarness(new LoopPipeline(), backend);
        loop.Ready(timeout.Token);
        loop.Publish(1);
        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => loop.Wait(timeout.Token)));
        Assert.Equal(1, backend.Executes);
    }

    [Fact]
    public void UnsignedIds_WorkPastLongMax_AndOverflowDoesNotWrap()
    {
        var backend = new LoopBackend();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var loop = new LoopHarness(new LoopPipeline(), backend, ulong.MaxValue - 1);
        loop.Ready(timeout.Token);
        loop.Publish(ulong.MaxValue);
        loop.Wait(timeout.Token);
        Assert.Throws<OverflowException>(() => loop.Publish(1));
    }

    [Fact]
    public void StopDuringOutput_ReleasesWaiterWithoutClaimingCompletion()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var backend = new LoopBackend
        {
            BeforeOutput = () =>
            {
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException();
            },
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var loop = new LoopHarness(new LoopPipeline(), backend);
        try
        {
            loop.Ready(timeout.Token);
            loop.Publish(1);
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            loop.Stop();
            Assert.ThrowsAny<OperationCanceledException>(() => loop.Wait(timeout.Token));
            Assert.ThrowsAny<OperationCanceledException>(() => loop.Publish(2));
        }
        finally
        {
            release.Set();
            loop.Dispose();
        }
    }

    [Fact]
    public void Frame_OrderIsRecordExecuteOutput_AndDoesNotWaitForGpu()
    {
        var order = new List<string>();
        var backend = new LoopBackend
        {
            OnExecute = () => order.Add("execute"),
            BeforeOutput = () => order.Add("output"),
        };
        var pipeline = new LoopPipeline { Record = _ => order.Add("record") };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var loop = new LoopHarness(pipeline, backend);
        loop.Ready(timeout.Token);
        loop.Publish(1);
        loop.Wait(timeout.Token);
        Assert.Equal(new[] { "record", "execute", "output" }, order);
        Assert.Equal(0, backend.IdleCalls); // Assert before Dispose, which may wait once for shutdown.
    }
}
