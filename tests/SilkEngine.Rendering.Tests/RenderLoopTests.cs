using SilkEngine.Rendering;
using Xunit;

namespace SilkEngine.Rendering.Tests;

[Collection(EngineHostCollection.Name)]
public sealed class RenderLoopTests
{
    [Fact]
    public void Completion_WaitsForExecute_AndRejectsOverwrite()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var backend = new LoopBackend
        {
            BeforeExecute = () =>
            {
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException();
            },
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
            Assert.Equal(1, backend.Presents);
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
    public void RecordingFailure_IsConsumed_AndTheNextFrameRuns()
    {
        var error = new InvalidOperationException("record failed");
        var backend = new LoopBackend();
        var pipeline = new LoopPipeline { Record = _ => throw error };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var loop = new LoopHarness(pipeline, backend);
        loop.Ready(timeout.Token);

        loop.Publish(1);
        loop.Wait(timeout.Token);
        Assert.False(loop.LastFrameSucceeded);
        Assert.Equal(0, backend.Executes);

        pipeline.Record = null;
        loop.Publish(2);
        loop.Wait(timeout.Token);
        Assert.True(loop.LastFrameSucceeded);
        Assert.Equal(1, backend.Executes);
    }

    [Fact]
    public void ExecuteFailure_IsConsumed_AndTheNextFrameRuns()
    {
        var error = new InvalidOperationException("execute failed");
        var backend = new LoopBackend { BeforeExecute = () => throw error };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var loop = new LoopHarness(new LoopPipeline(), backend);
        loop.Ready(timeout.Token);

        loop.Publish(1);
        loop.Wait(timeout.Token);
        Assert.False(loop.LastFrameSucceeded);
        Assert.Equal(0, backend.Executes);

        backend.BeforeExecute = null;
        loop.Publish(2);
        loop.Wait(timeout.Token);
        Assert.True(loop.LastFrameSucceeded);
        Assert.Equal(1, backend.Executes);
        Assert.Equal(1, backend.Presents);
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
    public void StopDuringExecute_ReleasesWaiterWithoutClaimingCompletion()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var backend = new LoopBackend
        {
            BeforeExecute = () =>
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
    public void Frame_OrderIsRecordExecutePresent_AndDoesNotWaitForGpu()
    {
        var order = new List<string>();
        var backend = new LoopBackend
        {
            BeforeExecute = () => order.Add("execute"),
            BeforePresent = () => order.Add("present"),
        };
        var pipeline = new LoopPipeline { Record = _ => order.Add("record") };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var loop = new LoopHarness(pipeline, backend);
        loop.Ready(timeout.Token);
        loop.Publish(1);
        loop.Wait(timeout.Token);
        Assert.Equal(new[] { "record", "execute", "present" }, order);
        Assert.Equal(0, backend.IdleCalls); // Assert before Dispose, which may wait once for shutdown.
    }

    [Fact]
    public void ConsecutiveFailedFrames_StopTheLoop()
    {
        var previousLimit = LoopHarness.FailureLimit;
        LoopHarness.FailureLimit = 3;
        var backend = new LoopBackend
        {
            BeforeExecute = () => throw new InvalidOperationException("execute failed"),
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var loop = new LoopHarness(new LoopPipeline(), backend);
        try
        {
            loop.Ready(timeout.Token);

            for (ulong frame = 1; frame <= 3; frame++)
            {
                loop.Publish(frame);
                loop.Wait(timeout.Token);
                Assert.False(loop.LastFrameSucceeded);
            }

            Assert.ThrowsAny<OperationCanceledException>(() =>
            {
                loop.Publish(4);
                loop.Wait(timeout.Token);
            });
        }
        finally
        {
            LoopHarness.FailureLimit = previousLimit;
            loop.Dispose();
        }
    }
}
