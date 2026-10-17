using System.Diagnostics;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Runtime.Host;
using Xunit;

namespace SilkEngine.Rendering.Tests;

[Collection(EngineHostCollection.Name)]
public sealed class MainLoopTimingTests
{
    private sealed class TimedMain : MainLoop
    {
        public override bool IsBackground => true;
        public readonly ManualResetEventSlim InFrameEnd = new();
        public readonly ManualResetEventSlim ReleaseFrameEnd = new();
        public readonly List<double> Deltas = new();
        public readonly List<ulong> FrameCounts = new();
        public readonly List<TimeSpan> PreviousDurations = new();

        public TimedMain(IRenderPipeline pipeline, IRenderBackend backend)
            : base(pipeline, backend, new NullGraphicsDevice()) { }

        protected override void OnFrameStarted()
        {
            base.OnFrameStarted();
            Deltas.Add(Time.UnscaledDeltaTime);
            FrameCounts.Add(Time.FrameCount);
            PreviousDurations.Add(LastFrameElapsed);
            if (Deltas.Count == 2)
                RequestStop();
        }

        protected override void OnFrameEnded()
        {
            InFrameEnd.Set();
            if (!ReleaseFrameEnd.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException();
        }
    }

    [Fact]
    public void Timing_ContainsRenderOutputAndFrameEnd_AndFeedsNextDelta()
    {
        using var inOutput = new ManualResetEventSlim();
        using var releaseOutput = new ManualResetEventSlim();
        var backend = new LoopBackend();
        backend.BeforeOutput = () =>
        {
            inOutput.Set();
            if (!releaseOutput.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException();
        };
        var main = new TimedMain(new LoopPipeline(), backend);
        main.Start();
        var containedInterval = new Stopwatch();
        try
        {
            Assert.True(inOutput.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(TimeSpan.Zero, main.LastFrameElapsed);
            containedInterval.Start();
            releaseOutput.Set();
            Assert.True(main.InFrameEnd.Wait(TimeSpan.FromSeconds(5)));
            Assert.Equal(TimeSpan.Zero, main.LastFrameElapsed);
            containedInterval.Stop();
            main.ReleaseFrameEnd.Set();
            Assert.True(SpinWait.SpinUntil(() => !main.IsAlive, TimeSpan.FromSeconds(5)));
            Assert.Equal(2, main.Deltas.Count);
            Assert.Equal(new ulong[] { 1, 2 }, main.FrameCounts);
            Assert.Equal(0, main.Deltas[0]);
            Assert.True(main.LastFrameElapsed >= containedInterval.Elapsed);
            Assert.Equal(main.PreviousDurations[1].TotalSeconds, main.Deltas[1]);
            Assert.Equal(main.PreviousDurations[1], main.LastFrameElapsed);
        }
        finally
        {
            releaseOutput.Set();
            main.ReleaseFrameEnd.Set();
            main.Dispose();
        }
    }

    [Fact]
    public void CancelingFirstFrame_DoesNotPublishSuccessfulDuration()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var backend = new LoopBackend();
        backend.BeforeOutput = () =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException();
        };
        var main = new TimedMain(new LoopPipeline(), backend);
        main.Start();
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            main.RequestStop();
            release.Set();
            Assert.True(SpinWait.SpinUntil(() => !main.IsAlive, TimeSpan.FromSeconds(5)));
            Assert.Equal(TimeSpan.Zero, main.LastFrameElapsed);
            Assert.False(main.InFrameEnd.IsSet);
        }
        finally
        {
            release.Set();
            main.ReleaseFrameEnd.Set();
            main.Dispose();
        }
    }
}
