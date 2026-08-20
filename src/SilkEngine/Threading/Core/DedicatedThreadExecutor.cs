using System;
using System.Threading;
using System.Threading.Tasks;
using SilkEngine.Core;

namespace SilkEngine.Threading;

/// <summary>
/// 专用线程执行者（长驻循环，如渲染）：ThreadFactory 创建线程，循环执行 frame；
/// frame 返回 false 或 Stop() 后线程退出。无任务队列（异步工作统一走 ThreadPoolExecutor）。
/// </summary>
public sealed class DedicatedThreadExecutor : ILoopExecutor
{
    private static int _idCounter;

    private readonly string _name;
    private readonly ThreadPriority _priority;
    private Thread _thread;
    private volatile bool _stopRequested;
    private Func<bool>? _frame;
    private readonly TaskCompletionSource _completed = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );

    /// <summary>执行者名称（申请时指定）。</summary>
    public string Name => _name;

    /// <summary>线程上下文（ctor 创建时登记）。</summary>
    public ThreadContext Context { get; private set; }

    /// <summary>专用线程是否存活（已 Run 且未退出）。</summary>
    public bool IsRunning => _thread?.IsAlive ?? false;

    /// <summary>创建专用线程执行者：经 ThreadFactory 创建后台线程并登记上下文（未启动，Run 后运行）。</summary>
    /// <param name="name">执行者名称</param>
    /// <param name="priority">线程优先级（默认 Normal）</param>
    public DedicatedThreadExecutor(string name, ThreadPriority priority = ThreadPriority.Normal)
    {
        _name = name;
        _priority = priority;
        var thread = ThreadFactory.CreateThread(Loop, _name, isBackground: true, _priority);
        Context = new ThreadContext(thread, (uint)Interlocked.Increment(ref _idCounter));
        _thread = thread;
    }

    /// <summary>启动循环：专用线程反复执行 frame；返回 false 或 Stop() 后退出。重复启动抛错。</summary>
    public IJobHandle Run(Func<bool> frame)
    {
        if (_thread is { IsAlive: true })
            throw new InvalidOperationException($"执行者 '{_name}' 已启动");

        _frame = frame;
        _stopRequested = false;
        _thread.Start();

        return new ExitHandle(this);
    }

    private void Loop()
    {
        while (!_stopRequested)
        {
            if (_frame is null || !_frame())
                break;
        }
        _completed.TrySetResult();
    }

    /// <summary>请求停止：置停止标志，等当前 frame 返回后线程退出。</summary>
    public void Stop() => _stopRequested = true;

    /// <summary>阻塞等线程结束（内建 2s 超时容错，避免 frame 阻塞时挂死）；未启动直接返回（幂等）。</summary>
    public void Join()
    {
        if ((_thread.ThreadState & ThreadState.Unstarted) != 0)
            return; // 从未启动：幂等（Request 后未 Initialize/未 Run 即 Shutdown 的场景）
        _thread.Join(2000);
    }

    public void Dispose()
    {
        Stop();
        Join();
    }

    /// <summary>线程退出时完成的句柄。</summary>
    private sealed class ExitHandle : IJobHandle
    {
        private readonly DedicatedThreadExecutor _owner;

        public ExitHandle(DedicatedThreadExecutor owner) => _owner = owner;

        public bool IsCompleted => !_owner.IsRunning;

        public void Wait() => _owner.Join();

        public ValueTask AsTask() => new(_owner._completed.Task);
    }
}
