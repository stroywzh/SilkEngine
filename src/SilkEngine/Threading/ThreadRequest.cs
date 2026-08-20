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
    /// <summary>执行者注册名（重名申请抛 InvalidOperationException）</summary>
    public readonly string Name;

    /// <summary>执行者种类（Dedicated=专用线程 / WorkerPool=共享 ThreadPool）</summary>
    public readonly ThreadKind Kind;

    /// <summary>申请数量（Dedicated 须为 1；WorkerPool 忽略）</summary>
    public readonly int Count;

    /// <summary>线程优先级（WorkerPool 忽略）</summary>
    public readonly ThreadPriority Priority;

    /// <summary>创建线程申请描述</summary>
    /// <param name="name">执行者注册名</param>
    /// <param name="kind">执行者种类</param>
    /// <param name="count">申请数量（默认 1）</param>
    /// <param name="priority">线程优先级（默认 Normal）</param>
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
