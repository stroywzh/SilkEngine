using SilkEngine.Threading;

namespace SilkEngine.Tests.Core.Assets;

/// <summary>同步假调度器：立即执行工作（Schedule 返回时结果已入完成队列），并统计调度次数</summary>
internal sealed class RecordingScheduler : IWorkerScheduler
{
    public int ScheduleCalls { get; private set; }

    public void Schedule(
        Func<Task> work,
        WorkPriority priority = WorkPriority.Normal,
        CancellationToken ct = default
    )
    {
        ScheduleCalls++;
        work().GetAwaiter().GetResult();
    }
}
