using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SilkEngine.Core;

namespace SilkEngine.Threading;

/// <summary>
/// 线程管理器（[Service] 自动注册，Priority -10000 = 基础设施最低值 → 最先注册 → Shutdown 反序最后释放）：
/// 主线程登记 + 亲和断言；统一申请入口 Request（决策层选择底层执行者）；
/// 默认提交委托 CoreCLR ThreadPool；Combine 跨执行者依赖聚合（ECS 编排预留）。
/// </summary>
[Service(-10000)]
public sealed class ThreadManager : IDisposable, IJobComposer
{
    private readonly List<IThreadExecutor> _executors = new();
    private readonly Dictionary<string, IThreadExecutor> _byName = new();
    private Thread? _mainThread;
    private ThreadPoolExecutor? _defaultExecutor;
    private bool _shutdown;

    public Thread? MainThread => _mainThread;

    /// <summary>当前调用是否为主线程（未登记时恒 false）。</summary>
    public bool IsMainThread =>
        _mainThread is not null && ReferenceEquals(Thread.CurrentThread, _mainThread);

    /// <summary>登记当前线程为主线程（引擎初始化调用）；只能登记一次。</summary>
    public void RegisterMainThread() => RegisterMainThread(Thread.CurrentThread);

    /// <summary>登记指定线程为主线程（测试可注入）；只能登记一次。</summary>
    public void RegisterMainThread(Thread thread)
    {
        if (_mainThread is not null)
            throw new InvalidOperationException("主线程只能登记一次");
        _mainThread = thread;
    }

    /// <summary>主线程亲和断言：非主线程调用抛 InvalidOperationException。</summary>
    public void AssertMainThread()
    {
        if (!IsMainThread)
            throw new InvalidOperationException("该调用必须发生在主线程");
    }

    /// <summary>统一申请入口：按 ThreadRequest 决策底层执行者并返回 T 类型句柄；转型失败或重名抛错。</summary>
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
            throw new InvalidOperationException(
                $"申请 '{request.Name}' ({request.Kind}) 无法转型为 {typeof(T).Name}（实际 {executor.GetType().Name}）");
        _byName.Add(request.Name, executor);
        _executors.Add(executor);
        return typed;
    }

    /// <summary>按名称查询执行者（调试/统计）。</summary>
    public bool TryGet(string name, out IThreadExecutor? executor)
    {
        if (_byName.TryGetValue(name, out var found))
        {
            executor = found;
            return true;
        }
        executor = null;
        return false;
    }

    /// <summary>默认提交：委托共享 ThreadPoolExecutor（CoreCLR ThreadPool）；关闭后抛错。</summary>
    public IJobHandle Submit(
        Func<CancellationToken, ValueTask> work,
        WorkPriority priority = WorkPriority.Normal,
        CancellationToken ct = default)
    {
        if (_shutdown)
            throw new InvalidOperationException("ThreadManager 已关闭");
        return DefaultExecutor.Submit(work, priority, ct);
    }

    /// <summary>依赖组合：全部依赖完成才完成（Task.WhenAll 聚合）。</summary>
    public IJobHandle Combine(params IJobHandle[] dependencies)
        => new TaskJobHandle(Task.WhenAll(dependencies.Select(d => d.AsTask().AsTask()).ToArray()));

    /// <summary>反序 Stop 并 Join 全部执行者（渲染等专用线程先停）；幂等。</summary>
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

    public void Dispose() => Shutdown();

    private ThreadPoolExecutor DefaultExecutor
    {
        get
        {
            if (_defaultExecutor is null)
            {
                _defaultExecutor = new ThreadPoolExecutor();
                _executors.Add(_defaultExecutor);
            }
            return _defaultExecutor;
        }
    }
}
