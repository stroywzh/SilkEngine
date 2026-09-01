using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using SilkEngine.Core;

namespace SilkEngine.Threading;

/// <summary>
/// 分阶段主线程派发器：每个阶段使用双队列，Drain 经 Interlocked.Exchange 取出当前批次，
/// 回调中新增的同阶段任务进入下一批次（批次边界，不无限延长当前批次）。
/// 关闭后 Post 快速失败；未执行的 InvokeAsync 以取消结束；单个回调异常经统一错误报告后继续执行剩余批次。
/// </summary>
internal sealed class MainThreadDispatcher : IMainThreadDispatcher, IDisposable
{
    private readonly IThreadGuard _guard;
    private readonly int _phaseCount = Enum.GetValues<MainThreadPhase>().Length;
    private readonly ConcurrentQueue<DispatchEntry>[] _current;
    private readonly ConcurrentQueue<DispatchEntry>[] _next;
    private int _disposed;

    /// <param name="guard">线程域守卫（Drain 断言 Main 域）</param>
    internal MainThreadDispatcher(IThreadGuard guard)
    {
        _guard = guard;
        _current = new ConcurrentQueue<DispatchEntry>[_phaseCount];
        _next = new ConcurrentQueue<DispatchEntry>[_phaseCount];
        for (var i = 0; i < _phaseCount; i++)
        {
            _current[i] = new ConcurrentQueue<DispatchEntry>();
            _next[i] = new ConcurrentQueue<DispatchEntry>();
        }
    }

    private sealed record DispatchEntry(Action Action, CancellationToken Token, TaskCompletionSource? Completion);

    /// <summary>投递回调到指定阶段；关闭后抛 InvalidOperationException。</summary>
    public void Post(MainThreadPhase phase, Action action)
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new InvalidOperationException("MainThreadDispatcher 已关闭");
        _current[(int)phase].Enqueue(new DispatchEntry(action, default, null));
    }

    /// <summary>投递回调并返回完成句柄；关闭或取消时以取消结束。</summary>
    public ValueTask InvokeAsync(MainThreadPhase phase, Action action, CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (Volatile.Read(ref _disposed) != 0)
        {
            completion.TrySetCanceled();
            return new ValueTask(completion.Task);
        }
        if (cancellationToken.IsCancellationRequested)
        {
            completion.TrySetCanceled(cancellationToken);
            return new ValueTask(completion.Task);
        }
        cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        _current[(int)phase].Enqueue(new DispatchEntry(action, cancellationToken, completion));
        return new ValueTask(completion.Task);
    }

    /// <summary>排空指定阶段当前批次（仅 Main 域调用）；批次内回调新增任务留待下一次 Drain。</summary>
    public void Drain(MainThreadPhase phase)
    {
        _guard.Assert(ThreadDomain.Main, $"Drain({phase})");
        var index = (int)phase;
        var batch = Interlocked.Exchange(ref _current[index], _next[index]);
        _next[index] = new ConcurrentQueue<DispatchEntry>();
        while (batch.TryDequeue(out var entry))
        {
            if (entry.Token.IsCancellationRequested)
            {
                entry.Completion?.TrySetCanceled(entry.Token);
                continue;
            }
            try
            {
                entry.Action();
                entry.Completion?.TrySetResult();
            }
            catch (Exception ex)
            {
                entry.Completion?.TrySetException(ex);
                Log.Error($"[MainThreadDispatcher] callback failed: {ex}");
            }
        }
    }

    /// <summary>关闭：拒绝新投递，未执行请求以取消结束；幂等。</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        for (var i = 0; i < _phaseCount; i++)
        {
            CancelRemaining(_current[i]);
            CancelRemaining(_next[i]);
        }
    }

    private static void CancelRemaining(ConcurrentQueue<DispatchEntry> queue)
    {
        while (queue.TryDequeue(out var entry))
            entry.Completion?.TrySetCanceled();
    }
}
