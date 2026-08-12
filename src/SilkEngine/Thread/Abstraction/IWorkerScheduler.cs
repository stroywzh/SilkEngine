using System;
using System.Threading;
using System.Threading.Tasks;

namespace SilkEngine.Threading;

public interface IWorkerScheduler
{
    void Schedule(Func<Task> work, WorkPriority priority = WorkPriority.Normal,
        CancellationToken ct = default);
}
