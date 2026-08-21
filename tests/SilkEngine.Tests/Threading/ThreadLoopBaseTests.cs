using SilkEngine.Threading;

namespace SilkEngine.Tests.Threading;

/// <summary>ThreadLoopBase 长期线程循环基类测试：优雅退出、唤醒、异常隔离、单属主、Dispose 竞态。</summary>
public class ThreadLoopBaseTests
{
    private sealed class RecordingExecutor : ILoopExecutor
    {
        public int StopCalls, JoinCalls;
        public Func<bool>? Frame;
        public string Name => "rec";
        public ThreadContext? Context => null;
        public IJobHandle Run(Func<bool> frame)
        {
            Frame = frame;
            return new TaskJobHandle(Task.CompletedTask);
        }

        public void Stop() => StopCalls++;
        public void Join() => JoinCalls++;
        public void Dispose() { }
    }

    /// <summary>同步驱动子类：WaitForWork 非阻塞（测试线程直接调 Frame）。</summary>
    private sealed class CounterLoop : ThreadLoopBase
    {
        public int Ticks;
        public bool ThrowOnce;

        public CounterLoop(ILoopExecutor exec) : base(exec) { }

        public void RunForTest() => Start();
        public bool RunFrame() => Frame();

        protected override void WaitForWork() { }

        protected override bool Tick()
        {
            if (ThrowOnce)
            {
                ThrowOnce = false;
                throw new InvalidOperationException("tick boom");
            }
            Ticks++;
            return Ticks < 5; // 5 轮后自然结束
        }
    }

    /// <summary>默认 WaitForWork 路径子类（阻塞 WaitAny，Wake/Stop 唤醒）。</summary>
    private sealed class DefaultWaitLoop : ThreadLoopBase
    {
        public int Ticks;

        public DefaultWaitLoop(ILoopExecutor exec) : base(exec) { }

        public void RunForTest() => Start();
        public bool RunFrame() => Frame();
        public void WakeTest() => Wake();

        protected override bool Tick()
        {
            Ticks++;
            return Ticks < 3;
        }
    }

    [Fact]
    public void Start_ExecutesTicks_UntilReturnFalse()
    {
        var exec = new RecordingExecutor();
        var loop = new CounterLoop(exec);
        loop.RunForTest();
        Assert.NotNull(exec.Frame); // Start 已绑定基类 Frame
        for (int i = 0; i < 10 && loop.RunFrame(); i++) { }
        Assert.Equal(5, loop.Ticks);
        loop.Dispose();
    }

    [Fact]
    public void RequestStop_ExitsGracefully()
    {
        var exec = new RecordingExecutor();
        var loop = new CounterLoop(exec);
        loop.RunForTest();
        Assert.True(loop.RunFrame());   // Tick 1
        loop.RequestStop();
        Assert.False(loop.RunFrame());  // 停止请求 → 退出
        Assert.Equal(1, loop.Ticks);
        loop.Dispose();
    }

    [Fact]
    public void TickException_IsIsolated_LoopContinues()
    {
        var exec = new RecordingExecutor();
        var loop = new CounterLoop(exec) { ThrowOnce = true };
        loop.RunForTest();
        Assert.True(loop.RunFrame());   // 抛错但循环继续
        Assert.True(loop.RunFrame());
        Assert.True(loop.Ticks >= 1);
        loop.Dispose();
    }

    [Fact]
    public void Dispose_DoesNotStopOrJoinExecutor()
    {
        var exec = new RecordingExecutor();
        var loop = new CounterLoop(exec);
        loop.RunForTest();
        loop.Dispose();
        Assert.Equal(0, exec.StopCalls); // 单属主：不触碰执行者
        Assert.Equal(0, exec.JoinCalls);
    }

    [Fact]
    public async Task Wake_UnblocksDefaultWaitForWork()
    {
        var exec = new RecordingExecutor();
        var loop = new DefaultWaitLoop(exec);
        loop.RunForTest();
        var t = Task.Run(loop.RunFrame);
        Thread.Sleep(100);
        Assert.False(t.IsCompleted);    // 阻塞在默认 WaitAny
        loop.WakeTest();                // 唤醒 → WaitForWork 返回
        Assert.True(await t.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(1, loop.Ticks);
        loop.Dispose();
    }

    [Fact]
    public async Task Dispose_WhileBlockedInWaitForWork_ReturnsFalseSafely()
    {
        var exec = new RecordingExecutor();
        var loop = new DefaultWaitLoop(exec);
        loop.RunForTest();
        var t = Task.Run(loop.RunFrame);
        Thread.Sleep(100);
        Assert.False(t.IsCompleted);    // 阻塞在 WaitAny
        loop.Dispose();                 // 事件释放 → ObjectDisposedException → 安全退出
        Assert.False(await t.WaitAsync(TimeSpan.FromSeconds(2)));
    }
}
