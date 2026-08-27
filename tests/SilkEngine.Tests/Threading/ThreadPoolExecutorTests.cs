using SilkEngine.Threading;
using Xunit;

namespace SilkEngine.Tests.Threading;

public class ThreadPoolExecutorTests
{
    [Fact]
    public void Submit_ExecutesWork()
    {
        using var exec = new ThreadPoolExecutor();
        int x = 0;
        var done = new ManualResetEventSlim(false);
        var job = exec.Submit(_ => { x = 42; done.Set(); return ValueTask.CompletedTask; });
        Assert.True(done.Wait(2000));
        job.Wait();
        Assert.Equal(42, x);
    }

    [Fact]
    public void Submit_Handle_IsCompletedAfterWait()
    {
        using var exec = new ThreadPoolExecutor();
        var job = exec.Submit(_ => ValueTask.CompletedTask);
        job.Wait();
        Assert.True(job.IsCompleted);
    }

    [Fact]
    public void Submit_AsTask_AwaitsCompletion()
    {
        using var exec = new ThreadPoolExecutor();
        int x = 0;
        var job = exec.Submit(async _ => { await Task.Yield(); x = 7; });
        job.AsTask().AsTask().Wait(2000);
        Assert.Equal(7, x);
    }

    [Fact]
    public void Submit_FailingWork_WaitPropagatesOriginalException()
    {
        using var exec = new ThreadPoolExecutor();
        var job = exec.Submit(_ => throw new InvalidOperationException("task-boom"));
        var ex = Assert.Throws<InvalidOperationException>(() => job.Wait());
        Assert.Equal("task-boom", ex.Message);
    }

    [Fact]
    public void StopJoinDispose_AreNoOps_AndIdempotent()
    {
        var exec = new ThreadPoolExecutor();
        exec.Stop();
        exec.Stop();
        exec.Join();
        exec.Dispose();
        exec.Dispose();
        Assert.Null(exec.Context);
        Assert.Equal("ThreadPool", exec.Name);
    }
}
