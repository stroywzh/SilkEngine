using System;
using System.Threading.Tasks;

namespace SilkEngine.Threading;

/// <summary>工作完成句柄：阻塞/异步等待 + 完成查询（ECS 依赖图的基础单元）。</summary>
public interface IJobHandle
{
    bool IsCompleted { get; }
    void Wait();
    ValueTask AsTask();
}
