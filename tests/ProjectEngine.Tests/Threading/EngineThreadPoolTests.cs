using ProjectEngine.Threading;

namespace ProjectEngine.Tests.Threading;

public class EngineThreadPoolTests
{
    [Fact]
    public void EnqueueWork_ExecutesTask()
    {
        using var pool = new EngineThreadPool(1);
        int x = 0;
        var done = new ManualResetEventSlim(false);
        pool.EnqueueWork(() => { x = 42; done.Set(); });
        done.Wait(2000);
        Assert.Equal(42, x);
    }

    [Fact]
    public void HighPriority_RunsBeforeNormal()
    {
        using var pool = new EngineThreadPool(1);
        var order = new List<string>();
        var done = new ManualResetEventSlim(false);
        var workerBusy = new ManualResetEventSlim(false);
        var release = new ManualResetEventSlim(false);

        pool.EnqueueWork(() => { workerBusy.Set(); release.Wait(); }, WorkPriority.Low);
        workerBusy.Wait(2000);
        pool.EnqueueWork(() => order.Add("Normal"), WorkPriority.Normal);
        pool.EnqueueWork(() => { order.Add("High"); done.Set(); }, WorkPriority.High);
        release.Set();

        done.Wait(2000);
        Assert.Equal("High", order[0]);
    }

    [Fact]
    public void CancelledToken_SkipsExecution()
    {
        using var pool = new EngineThreadPool(1);
        int x = 0;
        var done = new ManualResetEventSlim(false);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        pool.EnqueueWork(() => { x = 1; }, token: cts.Token);
        pool.EnqueueWork(() => done.Set());
        done.Wait(2000);
        Assert.Equal(0, x);
    }

    [Fact]
    public void Shutdown_ProcessesRemaining()
    {
        var pool = new EngineThreadPool(1);
        int x = 0;
        pool.EnqueueWork(() => x = 99);
        pool.Shutdown();
        Assert.Equal(99, x);
    }
}
