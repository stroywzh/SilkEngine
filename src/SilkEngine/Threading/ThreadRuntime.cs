using System;
using System.Collections.Generic;
using System.Threading;
using SilkEngine.Core;

namespace SilkEngine.Threading;

/// <summary>
/// 受管循环协议（internal 运行时协议）：持续扫描/监听/批量循环经 RegisterManagedLoop 登记，
/// 运行时关闭时统一 RequestStop 并 Join。
/// </summary>
internal interface IManagedLoop
{
    /// <summary>请求循环停止。</summary>
    void RequestStop();

    /// <summary>等待循环线程退出。</summary>
    void Join();
}

/// <summary>
/// 线程运行时：线程资源唯一属主。登记 Main 线程、经 AsyncLocal 标识受管 Worker 域、
/// 提供分阶段主线程派发与 Worker 调度、登记受管循环并负责关闭。
/// 不理解资产与渲染领域，不引用 AssetPipeline/AssetManager/Rendering 具体类型。
/// </summary>
[Service(-10000)]
public sealed class ThreadRuntime : IDisposable, IThreadGuard
{
    private readonly AsyncLocal<ThreadDomain> _ambient = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly List<IManagedLoop> _loops = [];
    private Thread? _mainThread;
    private int _disposed;

    /// <summary>创建运行时并装配主线程派发器与 Worker 调度器。</summary>
    public ThreadRuntime()
    {
        MainThread = new MainThreadDispatcher(this);
        Background = new BackgroundScheduler(this);
    }

    /// <summary>Worker 后台调度接口。</summary>
    public IBackgroundScheduler Background { get; }

    /// <summary>主线程阶段投递接口。</summary>
    public IMainThreadDispatcher MainThread { get; }

    /// <summary>运行时停止令牌（关闭时取消；Worker 链接此令牌）。</summary>
    internal CancellationToken StoppingToken => _stopping.Token;

    /// <summary>是否已关闭（关闭后拒绝新投递）。</summary>
    internal bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>测试观察用当前域。</summary>
    internal ThreadDomain CurrentDomainForTests => CurrentDomain;

    internal ThreadDomain CurrentDomain => ReferenceEquals(Thread.CurrentThread, _mainThread)
        ? ThreadDomain.Main
        : _ambient.Value;

    bool IThreadGuard.IsCurrent(ThreadDomain domain) => CurrentDomain == domain;

    ThreadDomain IThreadGuard.Current => CurrentDomain;

    void IThreadGuard.Assert(ThreadDomain expected, string operation)
    {
        if (CurrentDomain != expected)
            throw new ThreadDomainException(operation, expected, CurrentDomain);
    }

    /// <summary>登记当前线程为主线程（引擎初始化调用）；只能登记一次。</summary>
    /// <exception cref="InvalidOperationException">主线程已登记</exception>
    public void RegisterMainThread()
    {
        if (Interlocked.CompareExchange(ref _mainThread, Thread.CurrentThread, null) is not null)
            throw new InvalidOperationException("Main thread may only be registered once.");
    }

    /// <summary>进入指定域（AsyncLocal 作用域，跨 await 全程生效）。</summary>
    internal IDisposable Enter(ThreadDomain domain) => new DomainScope(_ambient, domain);

    /// <summary>进入 Render 域（Render 线程入口协议）。</summary>
    internal IDisposable EnterRender() => Enter(ThreadDomain.Render);

    /// <summary>登记受管循环；关闭时统一 RequestStop + Join。</summary>
    internal void RegisterManagedLoop(IManagedLoop loop) => _loops.Add(loop);

    /// <summary>排空主线程指定阶段（仅 Main 域调用）。</summary>
    internal void Drain(MainThreadPhase phase) => ((MainThreadDispatcher)MainThread).Drain(phase);

    /// <summary>
    /// 关闭：拒绝新投递 → 取消停止令牌 → 停止并 Join 受管循环 → 释放派发器。幂等。
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _stopping.Cancel();
        foreach (var loop in _loops)
        {
            loop.RequestStop();
            loop.Join();
        }
        ((IDisposable)MainThread).Dispose();
        _stopping.Dispose();
    }

    private sealed class DomainScope : IDisposable
    {
        private readonly AsyncLocal<ThreadDomain> _ambient;
        private readonly ThreadDomain _previous;

        public DomainScope(AsyncLocal<ThreadDomain> ambient, ThreadDomain domain)
        {
            _ambient = ambient;
            _previous = ambient.Value;
            ambient.Value = domain;
        }

        public void Dispose() => _ambient.Value = _previous;
    }
}
