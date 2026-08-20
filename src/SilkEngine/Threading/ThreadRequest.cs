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
    public readonly string Name;
    public readonly ThreadKind Kind;
    public readonly int Count;
    public readonly ThreadPriority Priority;

    public ThreadRequest(
        string name,
        ThreadKind kind,
        int count = 1,
        ThreadPriority priority = ThreadPriority.Normal
    )
    {
        this.Name = name;
        this.Kind = kind;
        this.Count = count;
        this.Priority = priority;
    }
}
