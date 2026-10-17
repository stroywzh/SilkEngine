using SilkEngine.Rendering.Abstraction;
using SilkEngine.Runtime;
using SilkEngine.Runtime.Host;
using Xunit;

namespace SilkEngine.Rendering.Tests;

[Collection(EngineHostCollection.Name)]
public sealed class MainLoopTimingTests
{
    /// <summary>Milliseconds parked inside the first frame's execution to prove duration containment.</summary>
    private const int BlockedInsideFrame = 150;

    [Fact]
    public void FrameDuration_ContainsExecuteWait_AndFeedsNextTimeValues()
    {
        using var firstEntered = new ManualResetEventSlim();
        using var releaseFirst = new ManualResetEventSlim();
        using var secondEntered = new ManualResetEventSlim();
        using var releaseSecond = new ManualResetEventSlim();
        var executes = 0;
        var backend = new LoopBackend
        {
            BeforeExecute = () =>
            {
                var call = Interlocked.Increment(ref executes);
                if (call == 1)
                {
                    firstEntered.Set();
                    if (!releaseFirst.Wait(TimeSpan.FromSeconds(5)))
                        throw new TimeoutException();
                }
                else if (call == 2)
                {
                    secondEntered.Set();
                    if (!releaseSecond.Wait(TimeSpan.FromSeconds(5)))
                        throw new TimeoutException();
                }
            },
        };
        var main = StartMainLoop(backend);
        try
        {
            Assert.True(firstEntered.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(TimeSpan.Zero, main.LastFrameElapsed);

            // Spend a known time inside frame one's execution, then assert the measured frame
            // duration contains it. A fixed sleep keeps this independent of scheduling jitter.
            Thread.Sleep(BlockedInsideFrame);
            releaseFirst.Set();
            Assert.True(secondEntered.Wait(TimeSpan.FromSeconds(5)));

            // The first frame is complete; the second frame is blocked inside execution,
            // so the published time values still describe the first frame.
            Assert.Equal(2UL, Time.FrameCount);
            Assert.True(main.LastFrameElapsed >= TimeSpan.FromMilliseconds(BlockedInsideFrame - 20));
            Assert.Equal(main.LastFrameElapsed.TotalSeconds, Time.UnscaledDeltaTime);
        }
        finally
        {
            releaseFirst.Set();
            releaseSecond.Set();
            RuntimeRegistry.StopMainThread();
        }
        Assert.False(main.IsAlive);
    }

    [Fact]
    public void CancelingFirstFrame_DoesNotPublishDuration()
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
        var main = StartMainLoop(backend);
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            main.RequestStop();
            release.Set();
            Assert.True(SpinWait.SpinUntil(() => !main.IsAlive, TimeSpan.FromSeconds(5)));
            Assert.Equal(TimeSpan.Zero, main.LastFrameElapsed);
            Assert.Equal(1UL, Time.FrameCount);
        }
        finally
        {
            release.Set();
            RuntimeRegistry.StopMainThread();
        }
    }

    [Fact]
    public void FailedFrame_DoesNotStopTheMainLoop_AndStillPublishesDuration()
    {
        using var secondEntered = new ManualResetEventSlim();
        using var releaseSecond = new ManualResetEventSlim();
        var executes = 0;
        var backend = new LoopBackend
        {
            BeforeExecute = () =>
            {
                var call = Interlocked.Increment(ref executes);
                if (call == 1)
                    throw new InvalidOperationException("execute failed");

                if (call == 2)
                {
                    secondEntered.Set();
                    if (!releaseSecond.Wait(TimeSpan.FromSeconds(5)))
                        throw new TimeoutException();
                }
            },
        };
        var main = StartMainLoop(backend);
        try
        {
            Assert.True(secondEntered.Wait(TimeSpan.FromSeconds(10)));

            // The failed first frame was consumed and timed; frame two is parked on the render
            // thread, so the published values still describe that failed frame.
            Assert.Equal(2UL, Time.FrameCount);
            Assert.True(main.LastFrameElapsed > TimeSpan.Zero);
            Assert.False(main.LastFrameSucceeded);
            Assert.True(main.IsAlive);
        }
        finally
        {
            releaseSecond.Set();
            RuntimeRegistry.StopMainThread();
        }
    }

    private static MainLoop StartMainLoop(LoopBackend backend)
    {
        Application.RenderPipeline = new LoopPipeline();
        Application.RenderBackend = backend;
        Application.GraphicsDevice = new NullGraphicsDevice();
        return ThreadFactory.BuildMainThread();
    }
}
