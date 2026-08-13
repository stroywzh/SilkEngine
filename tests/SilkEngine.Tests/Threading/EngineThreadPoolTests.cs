using SilkEngine;
using SilkEngine.Threading;
using System.Threading.Tasks;

namespace SilkEngine.Tests.Threading;

public class EngineThreadPoolTests
{
    private sealed class TestWriter : ILogWriter
    {
        private readonly List<string> _messages;
        public TestWriter(List<string> messages) => _messages = messages;
        public void Write(string msg) => _messages.Add(msg);
    }

    [Fact]
    public void EnqueueWork_ExecutesTask()
    {
        using var pool = new EngineThreadPool(1);
        int x = 0;
        var done = new ManualResetEventSlim(false);
        pool.EnqueueWork(() => { x = 42; done.Set(); return Task.CompletedTask; });
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

        pool.EnqueueWork(() => { workerBusy.Set(); release.Wait(); return Task.CompletedTask; }, WorkPriority.Low);
        workerBusy.Wait(2000);
        pool.EnqueueWork(() => { order.Add("Normal"); return Task.CompletedTask; }, WorkPriority.Normal);
        pool.EnqueueWork(() => { order.Add("High"); done.Set(); return Task.CompletedTask; }, WorkPriority.High);
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

        pool.EnqueueWork(() => { x = 1; return Task.CompletedTask; }, token: cts.Token);
        pool.EnqueueWork(() => { done.Set(); return Task.CompletedTask; });
        done.Wait(2000);
        Assert.Equal(0, x);
    }

    [Fact]
    public void Shutdown_ProcessesRemaining()
    {
        var pool = new EngineThreadPool(1);
        int x = 0;
        pool.EnqueueWork(() => { x = 99; return Task.CompletedTask; });
        pool.Shutdown();
        Assert.Equal(99, x);
    }

    [Fact]
    public void Schedule_ExecutesViaInterface()
    {
        IWorkerScheduler scheduler = new EngineThreadPool(1);
        int x = 0;
        var done = new ManualResetEventSlim(false);
        scheduler.Schedule(() => { x = 7; done.Set(); return Task.CompletedTask; });
        done.Wait(2000);
        Assert.Equal(7, x);
        ((EngineThreadPool)scheduler).Shutdown();
    }

    [Fact]
    public void Exception_DoesNotKillWorker()
    {
        using var pool = new EngineThreadPool(1);
        int x = 0;
        var done = new ManualResetEventSlim(false);
        pool.EnqueueWork(() => throw new InvalidOperationException("test"));
        pool.EnqueueWork(() => { x = 42; done.Set(); return Task.CompletedTask; });
        done.Wait(2000);
        Assert.Equal(42, x);
    }

    [Fact]
    public void EnqueueWork_AfterShutdown_IsDroppedWithWarning()
    {
        var pool = new EngineThreadPool(1);
        pool.Shutdown();
        var messages = new List<string>();
        var writer = new TestWriter(messages);
        Log.AddWriter(writer);
        try
        {
            pool.EnqueueWork(() => { throw new InvalidOperationException("must not run"); });
            Assert.Contains(messages, m => m.Contains("EnqueueWork"));
        }
        finally
        {
            Log.RemoveWriter(writer);
        }
    }
}
