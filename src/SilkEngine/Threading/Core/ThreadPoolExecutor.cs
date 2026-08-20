using System;
using System.Threading;
using System.Threading.Tasks;

namespace SilkEngine.Threading;

/// <summary>
/// CoreCLR ThreadPool 执行者（默认策略 + WorkerPool 申请）：Submit 委托 Task.Run。
/// 无自有线程（Context=null），Stop/Join 为 no-op；WorkPriority 当前忽略（文档：未来 ECS 策略使用）。
/// </summary>
public sealed class ThreadPoolExecutor : ITaskExecutor, SilkEngine.Core.ITaskScheduler
{
    /// <summary>执行者名称（固定 "ThreadPool"）。</summary>
    public string Name => "ThreadPool";

    /// <summary>无自有线程（CoreCLR ThreadPool 由运行时管理）→ 恒 null。</summary>
    public ThreadContext? Context => null;

    void SilkEngine.Core.ITaskScheduler.Submit(Func<CancellationToken, ValueTask> work) =>
        Submit(work, WorkPriority.Normal);

    /// <summary>提交异步工作到 CoreCLR ThreadPool（Task.Run）。</summary>
    /// <param name="work">异步工作委托（接收取消令牌）</param>
    /// <param name="priority">工作优先级（当前忽略）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>工作完成句柄</returns>
    public IJobHandle Submit(
        Func<CancellationToken, ValueTask> work,
        WorkPriority priority = WorkPriority.Normal,
        CancellationToken ct = default)
        => new TaskJobHandle(Task.Run(async () => await work(ct).ConfigureAwait(false), ct));

    /// <summary>no-op：线程归 CoreCLR ThreadPool 管理，无自有线程可停。</summary>
    public void Stop() { }

    /// <summary>no-op：无自有线程需等待。</summary>
    public void Join() { }

    /// <summary>no-op：无可释放资源。</summary>
    public void Dispose() { }
}
