using SilkEngine.Core;
using SilkEngine.Threading;

namespace SilkEngine.Tests.Core.Assets;

/// <summary>同步假调度器：立即执行工作（Submit 返回时结果已入完成队列），并统计调度次数</summary>
internal sealed class RecordingScheduler : ITaskExecutor, ITaskScheduler
{
    public int ScheduleCalls { get; private set; }
    public string Name => "RecordingScheduler";
    public ThreadContext? Context => null;
    public void Stop() { }
    public void Join() { }
    public void Dispose() { }

    void ITaskScheduler.Submit(Func<CancellationToken, ValueTask> work) =>
        Submit(work, WorkPriority.Normal);

    public IJobHandle Submit(
        Func<CancellationToken, ValueTask> work,
        WorkPriority priority = WorkPriority.Normal,
        CancellationToken ct = default)
    {
        ScheduleCalls++;
        work(ct).GetAwaiter().GetResult();
        return new TaskJobHandle(Task.CompletedTask);
    }
}
