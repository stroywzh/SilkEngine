using System;
using System.Threading;
using System.Threading.Tasks;

namespace SilkEngine.Core;

/// <summary>最小任务调度抽象（Core 基础设施；Threading 层实现，依赖方向 Threading→Core）</summary>
public interface ITaskScheduler
{
    void Submit(Func<CancellationToken, ValueTask> work);
}
