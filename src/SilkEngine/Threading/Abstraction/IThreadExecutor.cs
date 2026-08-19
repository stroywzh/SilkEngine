using System;
using System.Threading;

namespace SilkEngine.Threading;

/// <summary>工作优先级（当前 Task 实现忽略，未来 ECS 批处理策略使用）。</summary>
public enum WorkPriority
{
    Low,
    Normal,
    High,
}

public interface IThreadContext : IDisposable
{
    string Name { get; }
    ThreadContext Context { get; } // ThreadPool 策略无自有线程 → null
}

/// <summary>
/// 线程执行者最上层通用接口：任何线程执行者的最小共性（元数据 + 生命周期）。
/// 底层策略（专用线程 / CoreCLR ThreadPool / 未来 ECS）不向调用方暴露。
/// </summary>
public interface IThreadExecutor : IThreadContext
{
    void Stop(); // 请求停止（幂等；排空语义由各策略保证）
    void Join(); // 阻塞等线程结束（容错超时，不无限挂起）
}

/// <summary>任务提交能力：提交单个异步工作项，返回完成句柄。</summary>
public interface ITaskExecutor : IThreadExecutor
{
    IJobHandle Submit(
        Func<CancellationToken, ValueTask> work,
        WorkPriority priority = WorkPriority.Normal,
        CancellationToken ct = default
    );
}

/// <summary>批处理能力（ECS JobSystem 预留）：同构工作批并行执行。</summary>
public interface IBatchExecutor : ITaskExecutor
{
    IJobHandle SubmitBatch<T>(
        ReadOnlyMemory<T> jobs,
        Action<T> body,
        WorkPriority priority = WorkPriority.Normal,
        CancellationToken ct = default
    );
}

/// <summary>长驻循环能力：专用线程循环执行 frame，返回 false 或 Stop() 后退出。</summary>
public interface ILoopExecutor : IThreadExecutor
{
    IJobHandle Run(Func<bool> frame);
}

public interface IThreadLoop
{
    private static ILoopExecutor _executor { get; set; }
    static ILoopExecutor ThreadLoop => _executor;
}
