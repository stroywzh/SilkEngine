using System;
using System.Threading;

namespace SilkEngine.Threading;

/// <summary>工作优先级</summary>
public enum WorkPriority
{
    /// <summary>低优先级（当前实现忽略，ECS 策略预留）</summary>
    Low,

    /// <summary>默认优先级</summary>
    Normal,

    /// <summary>高优先级（当前实现忽略，ECS 策略预留）</summary>
    High,
}

/// <summary>线程执行者元数据：名称 + 线程上下文（Dispose 契约见各实现）。</summary>
public interface IThreadContext : IDisposable
{
    /// <summary>执行者名称（注册/日志用）</summary>
    string Name { get; }

    /// <summary>线程上下文；ThreadPool 策略无自有线程 → null</summary>
    ThreadContext Context { get; }
}

/// <summary>
/// 线程执行者最上层通用接口
/// <br/>任何线程执行者的最小共性（元数据 + 生命周期）
/// <br/>底层策略（专用线程 / CoreCLR ThreadPool / 未来 ECS）不向调用方暴露
/// </summary>
public interface IThreadExecutor : IThreadContext
{
    /// <summary>请求停止（幂等；排空语义由各策略保证）。</summary>
    void Stop();

    /// <summary>阻塞等待线程结束（容错超时，不无限挂起）。</summary>
    void Join();
}

/// <summary>一般任务执行接口，提交后返回对应任务的Handler</summary>
public interface ITaskExecutor : IThreadExecutor
{
    /// <summary>提交异步工作，返回完成句柄。</summary>
    /// <param name="work">异步工作委托（接收取消令牌）</param>
    /// <param name="priority">工作优先级（当前忽略）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>工作完成句柄</returns>
    IJobHandle Submit(
        Func<CancellationToken, ValueTask> work,
        WorkPriority priority = WorkPriority.Normal,
        CancellationToken ct = default
    );
}

/// <summary>批处理能力（ECS JobSystem 预留）：同构工作批并行执行。</summary>
public interface IBatchExecutor : ITaskExecutor
{
    /// <summary>批量提交同构工作（实现者可并行执行）。</summary>
    /// <typeparam name="T">工作项类型</typeparam>
    /// <param name="jobs">工作项内存区</param>
    /// <param name="body">每项执行委托</param>
    /// <param name="priority">工作优先级（当前忽略）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>整批完成句柄</returns>
    IJobHandle SubmitBatch<T>(
        ReadOnlyMemory<T> jobs,
        Action<T> body,
        WorkPriority priority = WorkPriority.Normal,
        CancellationToken ct = default
    );
}

/// <summary>
/// 长期线程循环接口
/// <br/>专用线程循环执行 frame，返回 false 或 Stop() 后退出
/// </summary>
public interface ILoopExecutor : IThreadExecutor
{
    /// <summary>启动长期循环：专用线程反复执行 frame；返回 false 或 Stop() 后退出。</summary>
    /// <param name="frame">单帧回调（返回 false 退出循环）</param>
    /// <returns>线程退出时完成的句柄</returns>
    IJobHandle Run(Func<bool> frame);
}
