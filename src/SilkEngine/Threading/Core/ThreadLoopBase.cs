using System;
using System.Threading;
using SilkEngine.Core;

namespace SilkEngine.Threading;

/// <summary>
/// 长期线程循环基类：统一线程生命周期（执行者注入、优雅退出、唤醒、异常隔离）。
/// 不创建/Stop/Join 线程——执行者生命周期归 ThreadManager（单属主）。
/// 未来长期线程（Log drain、资产预加载、ECS 工作线程）继承复用。
/// </summary>
public abstract class ThreadLoopBase : IDisposable
{
    private readonly ILoopExecutor _executor;
    private volatile bool _running;
    private readonly ManualResetEventSlim _wake = new(false);
    private readonly ManualResetEventSlim _stop = new(false);
    private bool _disposed;

    /// <summary>创建长期循环基类。</summary>
    /// <param name="executor">执行者（ThreadManager 申请获得；生命周期归 ThreadManager）</param>
    /// <exception cref="ArgumentNullException">executor 为 null</exception>
    protected ThreadLoopBase(ILoopExecutor executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    /// <summary>已绑定的执行者（仅供派生类读取元数据；生命周期归 ThreadManager）。</summary>
    protected ILoopExecutor Executor => _executor;

    /// <summary>绑定执行者并启动循环（Run(Frame)，返回 false 退出）。</summary>
    protected void Start()
    {
        _running = true;
        _executor.Run(Frame);
    }

    /// <summary>循环框架：退出检查 → 等待工作 → 业务 Tick（异常隔离，不杀线程）。</summary>
    internal bool Frame()
    {
        if (!_running)
            return false;
        try
        {
            WaitForWork();
        }
        catch (ObjectDisposedException)
        {
            return false; // Dispose 竞态：安全退出
        }
        if (!_running)
            return false;
        try
        {
            return Tick();
        }
        catch (Exception ex)
        {
            Log.Error($"[{GetType().Name}] loop tick failed: {ex}");
            return true; // 异常隔离：循环继续
        }
    }

    /// <summary>等待工作信号；默认等待 Wake/Stop；业务可覆写为 WaitAny(业务信号, StopEvent)。</summary>
    protected virtual void WaitForWork()
        => WaitHandle.WaitAny(new[] { _wake.WaitHandle, _stop.WaitHandle });

    /// <summary>业务循环体（每轮调用；返回 false 结束循环）。</summary>
    protected abstract bool Tick();

    /// <summary>唤醒阻塞的工作等待（业务信号就绪时调用）。</summary>
    protected void Wake() => _wake.Set();

    /// <summary>停止令牌（供业务 WaitAny 组合）。</summary>
    protected WaitHandle StopEvent => _stop.WaitHandle;

    /// <summary>请求停止（幂等；不 Join——停止属主 ThreadManager.Shutdown）。</summary>
    public void RequestStop()
    {
        _running = false;
        _wake.Set();
        _stop.Set();
    }

    /// <summary>释放基类信号（幂等；不触碰执行者）。</summary>
    public virtual void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        RequestStop();
        _wake.Dispose();
        _stop.Dispose();
    }
}
