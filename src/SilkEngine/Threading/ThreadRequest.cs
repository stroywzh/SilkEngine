using System.Threading;

namespace SilkEngine.Threading;

/// <summary>申请的执行者种类。</summary>
public enum ThreadKind
{
    /// <summary>专用线程（长驻循环，如渲染）；Count 必须为 1。</summary>
    Dedicated,

    /// <summary>共享 Task 执行者（CoreCLR ThreadPool）；Count/Priority 当前忽略。</summary>
    WorkerPool,
}

/// <summary>线程申请描述：名称 + 种类 + 数量 + 优先级（决策层据此选择底层策略）。</summary>
public readonly record struct ThreadRequest
{
    public readonly string name;
    public readonly ThreadKind kind;
    public readonly int count;
    public readonly ThreadPriority priority;

    public ThreadRequest(
        string name,
        ThreadKind kind,
        int count = 1,
        ThreadPriority priority = ThreadPriority.Normal
    )
    {
        this.name = name;
        this.kind = kind;
        this.count = count;
        this.priority = priority;
    }

    bool EnsureCount(int count)
    {
        return (kind) switch
        {
            ThreadKind.Dedicated => count == 1,
            ThreadKind.WorkerPool => count >= 1,
            _ => false,
        };
    }
}
