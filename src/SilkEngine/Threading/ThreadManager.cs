using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SilkEngine.Core;

namespace SilkEngine.Threading;

/// <summary>
/// 线程管理器（[Service] 自动注册，Priority -10000 = 基础设施最低值 → 最先注册 → Shutdown 反序最后释放）：
/// 主线程登记 + 亲和断言；统一申请入口 Request（决策层选择底层执行者）；
/// 默认提交委托 CoreCLR ThreadPool；Combine 跨执行者依赖聚合（ECS 编排预留）。
/// 执行者单属主：创建后的 Stop/Join 仅经本类（Shutdown 反序执行），外部不得自行停用。
/// </summary>
[Service(-10000)]
public sealed class ThreadManager : IDisposable, IJobComposer
{
    private readonly List<IThreadExecutor> _executors = new();
    private readonly Dictionary<string, IThreadExecutor> _byName = new();
    private Thread? _mainThread;
    private Lazy<ThreadPoolExecutor> _defaultExecutor = new();
    private bool _shutdown;

    /// <summary>当前调用是否为主线程（未登记时恒 false）。</summary>
    public bool IsMainThread =>
        _mainThread is not null && ReferenceEquals(Thread.CurrentThread, _mainThread);

    /// <summary>登记当前线程为主线程（引擎初始化调用）；只能登记一次。</summary>
    /// <exception cref="InvalidOperationException">主线程已登记</exception>
    public void RegisterMainThread() => RegisterMainThread(Thread.CurrentThread);

    /// <summary>登记指定线程为主线程（测试可注入）；只能登记一次。</summary>
    /// <param name="thread">要登记为主线程的线程</param>
    /// <exception cref="InvalidOperationException">主线程已登记</exception>
    public void RegisterMainThread(Thread thread)
    {
        if (_mainThread is not null)
            throw new InvalidOperationException("主线程只能登记一次");
        _mainThread = thread;
    }

    /// <summary>主线程亲和断言：非主线程调用抛异常。</summary>
    /// <exception cref="InvalidOperationException">当前线程非登记的主线程</exception>
    public void AssertMainThread()
    {
        if (!IsMainThread)
            throw new InvalidOperationException("该调用必须发生在主线程");
    }

    /// <summary>
    /// 统一线程申请入口：按 ThreadRequest 决策底层执行者并返回 T 类型句柄
    /// （Dedicated 创建专用线程执行者；WorkerPool 复用共享 ThreadPool 执行者）。
    /// </summary>
    /// <typeparam name="T">返回句柄类型（须与执行者实际类型一致）</typeparam>
    /// <param name="request">申请描述（名称/种类/数量/优先级）</param>
    /// <returns>已注册的 T 类型执行者句柄</returns>
    /// <exception cref="InvalidOperationException">Manager Shutdown 后申请；申请名已注册；执行者无法转型为 T</exception>
    public T Request<T>(ThreadRequest request)
        where T : class, IThreadExecutor
    {
        if (_shutdown)
            throw new InvalidOperationException("ThreadManager 已关闭");
        if (_byName.ContainsKey(request.Name))
            throw new InvalidOperationException($"线程执行者 '{request.Name}' 已注册");
        IThreadExecutor executor = request.Kind switch
        {
            ThreadKind.Dedicated => new DedicatedThreadExecutor(request.Name, request.Priority),
            _ => DefaultExecutor,
        };

        if (executor is not T typed)
        {
            executor.Dispose();
            throw new InvalidOperationException(
                $"申请 '{request.Name}' ({request.Kind}) 无法转型为 {typeof(T).Name}（实际 {executor.GetType().Name}）"
            );
        }

        _byName.Add(request.Name, executor);
        _executors.Add(executor);
        return typed;
    }

    /// <summary>按名称查询执行者；未找到返回 false 且 executor 为 null。</summary>
    /// <param name="name">执行者注册名</param>
    /// <param name="executor">找到的执行者；未找到为 null</param>
    /// <returns>是否找到</returns>
    public bool TryGet(string name, out IThreadExecutor executor)
    {
        if (_byName.TryGetValue(name, out var found))
        {
            executor = found;
            return true;
        }
        executor = null!;
        return false;
    }

    /// <summary>默认提交：委托共享 ThreadPoolExecutor（CoreCLR ThreadPool）。</summary>
    /// <param name="work">异步工作委托（接收取消令牌）</param>
    /// <param name="priority">工作优先级（当前忽略）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>工作完成句柄</returns>
    /// <exception cref="InvalidOperationException">ThreadManager Shutdown 后提交</exception>
    public IJobHandle Submit(
        Func<CancellationToken, ValueTask> work,
        WorkPriority priority = WorkPriority.Normal,
        CancellationToken ct = default
    )
    {
        if (_shutdown)
            throw new InvalidOperationException("ThreadManager 已关闭");
        return DefaultExecutor.Submit(work, priority, ct);
    }

    /// <summary>依赖组合：全部依赖完成才完成（Task.WhenAll 单次聚合包装）。</summary>
    /// <param name="dependencies">依赖句柄数组</param>
    /// <returns>聚合完成句柄（任一依赖失败则失败）</returns>
    public IJobHandle Combine(params IJobHandle[] dependencies)
    {
        Task[] tasks = new Task[dependencies.Length];
        for (var i = 0; i < dependencies.Length; i++)
            tasks[i] = dependencies[i].AsTask().AsTask();
        return new TaskJobHandle(Task.WhenAll(tasks));
    }

    /// <summary>
    /// 反序 Stop 并 Join 全部执行者（渲染等专用线程先停）
    /// <br/>幂等。</summary>
    public void Shutdown()
    {
        if (_shutdown)
            return;
        _shutdown = true;
        for (var i = _executors.Count - 1; i >= 0; i--)
            _executors[i].Stop();
        for (var i = _executors.Count - 1; i >= 0; i--)
            _executors[i].Join();
        _executors.Clear();
        _byName.Clear();
    }

    /// <summary>释放：等价 Shutdown()（幂等）。</summary>
    public void Dispose() => Shutdown();

    private ThreadPoolExecutor DefaultExecutor
    {
        get => _defaultExecutor.Value;
    }
}
