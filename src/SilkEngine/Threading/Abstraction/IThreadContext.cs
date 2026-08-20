namespace SilkEngine.Threading;

/// <summary>
/// 线程上下文，用于记录具体线程的信息
/// </summary>
public record ThreadContext
{
    public Thread Thread { get; init; }

    /// <summary>
    /// 线程名称
    /// </summary>
    public string Name => Thread.Name ?? $"UnNamed-ManagedThread-{InternalManagedId}";

    /// <summary>
    /// OS提供的线程PID
    /// </summary>
    public int NativeThreadId => Thread.GetCurrentProcessorId();

    /// <summary>
    /// 内部管理ID
    /// </summary>
    public uint InternalManagedId { get; internal set; }

    public bool IsBackground => Thread.IsBackground;
    public ThreadPriority Priority => Thread.Priority;
    public CancellationToken CancellationToken { get; init; }

    public ThreadContext(Thread thread, uint internalId)
    {
        this.Thread = thread;
        this.InternalManagedId = internalId;
    }

    public override string ToString()
    {
        return $"-ThreadContext:Name{Name}\n |NativeThreadId{NativeThreadId}-InternalId{InternalManagedId}\n |IsBackGround{IsBackground}-Priority{Priority}";
    }
}

// TODO:线程负载接口，我感觉不需要这个东西，但是可以留着，因为现在的多线程不需要高并发，后面可以做成类似Unity的JobHandler
// 但是现在的Worker本质上是建立一个Thread，然后在backgroundThread里面同步阻塞获取Task的内容
public interface IWorkload
{
    string Name { get; }
    void Submit(object workload);
    void Remove(object workload);
    bool ExecuteFrame(ThreadContext context); // 工作线程循环体，返回 false 退出
}
