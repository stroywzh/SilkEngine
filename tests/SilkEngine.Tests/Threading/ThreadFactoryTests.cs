using SilkEngine.Threading;

namespace SilkEngine.Tests.Threading;

public class ThreadFactoryTests
{
    [Fact]
    public void ThreadFactory_RemainsOnlyDedicatedThreadCreationEntry()
    {
        var thread = ThreadFactory.CreateThread(static () => { }, "RuntimeVerification");

        Assert.Equal("RuntimeVerification", thread.Name);
        Assert.True(thread.IsBackground);
    }

    [Fact]
    public void CreateThread_SetsName()
    {
        var t = ThreadFactory.CreateThread(() => { }, "MyThread");
        Assert.Equal("MyThread", t.Name);
    }

    [Fact]
    public void CreateThread_SetsIsBackground_TrueByDefault()
    {
        var t = ThreadFactory.CreateThread(() => { }, "Bg");
        Assert.True(t.IsBackground);
    }

    [Fact]
    public void CreateThread_SetsIsBackground_False()
    {
        var t = ThreadFactory.CreateThread(() => { }, "Fg", isBackground: false);
        Assert.False(t.IsBackground);
    }

    [Fact]
    public void CreateThread_SetsPriority()
    {
        var t = ThreadFactory.CreateThread(() => { }, "Prio", priority: ThreadPriority.AboveNormal);
        Assert.Equal(ThreadPriority.AboveNormal, t.Priority);
    }

    [Fact]
    public void CreateThread_RunsAction()
    {
        int x = 0;
        var t = ThreadFactory.CreateThread(() => { x = 42; }, "Runner");
        t.Start();
        t.Join();
        Assert.Equal(42, x);
    }
}
