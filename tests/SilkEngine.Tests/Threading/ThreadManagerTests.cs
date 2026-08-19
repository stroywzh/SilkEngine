using SilkEngine.Threading;
using Xunit;

namespace SilkEngine.Tests.Threading;

public class ThreadManagerTests
{
    [Fact]
    public void RegisterMainThread_RegistersOnce_SecondThrows()
    {
        var tm = new ThreadManager();
        tm.RegisterMainThread();
        Assert.Throws<InvalidOperationException>(() => tm.RegisterMainThread());
    }

    [Fact]
    public void IsMainThread_TrueOnlyForRegisteredThread()
    {
        var tm = new ThreadManager();
        Assert.False(tm.IsMainThread);   // 未登记
        tm.RegisterMainThread(Thread.CurrentThread);
        Assert.True(tm.IsMainThread);
    }

    [Fact]
    public void AssertMainThread_Unregistered_Throws()
    {
        var tm = new ThreadManager();
        Assert.Throws<InvalidOperationException>(() => tm.AssertMainThread());
    }

    [Fact]
    public void Request_Dedicated_ReturnsLoopExecutor_RegisteredByName()
    {
        using var tm = new ThreadManager();
        var exec = tm.Request<ILoopExecutor>(new("RenderThread", ThreadKind.Dedicated));
        Assert.Equal("RenderThread", exec.Name);
        Assert.True(tm.TryGet("RenderThread", out var found));
        Assert.Same(exec, found);
    }

    [Fact]
    public void Request_WorkerPool_ReturnsSharedDefaultExecutor()
    {
        using var tm = new ThreadManager();
        var a = tm.Request<ITaskExecutor>(new("Workers", ThreadKind.WorkerPool, Count: 2));
        var b = tm.Request<ITaskExecutor>(new("Other", ThreadKind.WorkerPool));
        Assert.Same(a, b);   // 共享单例（CoreCLR ThreadPool）
    }

    [Fact]
    public void Request_DuplicateName_Throws()
    {
        using var tm = new ThreadManager();
        tm.Request<ILoopExecutor>(new("Render", ThreadKind.Dedicated));
        Assert.Throws<InvalidOperationException>(
            () => tm.Request<ILoopExecutor>(new("Render", ThreadKind.Dedicated)));
    }

    [Fact]
    public void Request_WrongGeneric_Throws()
    {
        using var tm = new ThreadManager();
        Assert.Throws<InvalidOperationException>(
            () => tm.Request<IBatchExecutor>(new("R", ThreadKind.Dedicated)));
    }

    [Fact]
    public void Submit_DefaultExecutor_ExecutesWork()
    {
        using var tm = new ThreadManager();
        int x = 0;
        var done = new ManualResetEventSlim(false);
        var job = tm.Submit(_ => { x = 1; done.Set(); return ValueTask.CompletedTask; });
        Assert.True(done.Wait(2000));
        job.Wait();
        Assert.Equal(1, x);
    }

    [Fact]
    public void Combine_CompletesWhenAllComplete()
    {
        using var tm = new ThreadManager();
        var done1 = new ManualResetEventSlim(false);
        var done2 = new ManualResetEventSlim(false);
        var j1 = tm.Submit(_ => { Thread.Sleep(30); done1.Set(); return ValueTask.CompletedTask; });
        var j2 = tm.Submit(_ => { done2.Set(); return ValueTask.CompletedTask; });
        var combined = tm.Combine(j1, j2);
        combined.Wait();
        Assert.True(combined.IsCompleted);
        Assert.True(done1.IsSet && done2.IsSet);
    }

    [Fact]
    public void Shutdown_StopsExecutors_IsIdempotent()
    {
        var tm = new ThreadManager();
        int frames = 0;
        var exec = tm.Request<ILoopExecutor>(new("Loop", ThreadKind.Dedicated));
        var job = exec.Run(() => { frames++; Thread.Sleep(10); return true; });
        Thread.Sleep(30);
        tm.Shutdown();
        tm.Shutdown();   // 幂等
        job.Wait();
        Assert.True(job.IsCompleted);
    }

    [Fact]
    public void Request_AfterShutdown_Throws()
    {
        var tm = new ThreadManager();
        tm.Shutdown();
        Assert.Throws<InvalidOperationException>(
            () => tm.Request<ITaskExecutor>(new("W", ThreadKind.WorkerPool)));
    }
}
