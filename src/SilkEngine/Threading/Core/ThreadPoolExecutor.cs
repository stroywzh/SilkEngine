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
    public string Name => "ThreadPool";
    public ThreadContext? Context => null;

    void SilkEngine.Core.ITaskScheduler.Submit(Func<CancellationToken, ValueTask> work) =>
        Submit(work, WorkPriority.Normal);

    public IJobHandle Submit(
        Func<CancellationToken, ValueTask> work,
        WorkPriority priority = WorkPriority.Normal,
        CancellationToken ct = default)
        => new TaskJobHandle(Task.Run(async () => await work(ct).ConfigureAwait(false), ct));

    public void Stop() { }
    public void Join() { }
    public void Dispose() { }
}
