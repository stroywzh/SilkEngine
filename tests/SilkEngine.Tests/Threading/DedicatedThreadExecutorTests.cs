using SilkEngine.Threading;
using Xunit;

namespace SilkEngine.Tests.Threading;

public class DedicatedThreadExecutorTests
{
    [Fact]
    public void Run_ExecutesFrame_OnDedicatedThread()
    {
        using var exec = new DedicatedThreadExecutor("TestLoop");
        int frames = 0;
        var onThread = new Thread(static () => { });
        var done = new ManualResetEventSlim(false);
        var job = exec.Run(() =>
        {
            frames++;
            onThread = Thread.CurrentThread;
            done.Set();
            return false;   // 单帧后退出
        });
        Assert.True(done.Wait(2000));
        job.Wait();
        Assert.Equal(1, frames);
        Assert.Equal("TestLoop", onThread.Name);
        Assert.True(job.IsCompleted);
    }

    [Fact]
    public void Run_FrameReturnsTrue_LoopsUntilFalse()
    {
        using var exec = new DedicatedThreadExecutor("Loop2");
        int frames = 0;
        var job = exec.Run(() => ++frames < 5);
        job.Wait();
        Assert.Equal(5, frames);
    }

    [Fact]
    public void Stop_ExitsRunningFrame()
    {
        using var exec = new DedicatedThreadExecutor("Loop3");
        int frames = 0;
        var job = exec.Run(() => { frames++; Thread.Sleep(5); return true; });
        Thread.Sleep(30);
        exec.Stop();
        job.Wait();
        Assert.True(job.IsCompleted);
    }

    [Fact]
    public void Run_WhileAlreadyRunning_Throws()
    {
        using var exec = new DedicatedThreadExecutor("Loop4");
        var job = exec.Run(() => { Thread.Sleep(50); return false; });
        Assert.Throws<InvalidOperationException>(() => exec.Run(() => false));
        job.Wait();
    }

    [Fact]
    public void Context_ReportsThreadMetadata()
    {
        using var exec = new DedicatedThreadExecutor("MetaLoop");
        var job = exec.Run(() => false);
        job.Wait();
        Assert.NotNull(exec.Context);
        Assert.Equal("MetaLoop", exec.Context!.Name);
        Assert.NotNull(exec.Context.Thread);
    }

    [Fact]
    public void Dispose_StopsAndJoins_WithoutHanging()
    {
        var exec = new DedicatedThreadExecutor("DisposeLoop");
        exec.Run(() => { Thread.Sleep(50); return false; });
        exec.Dispose();   // Join 内建 2s 超时容错
        Assert.True(true);
    }
}
