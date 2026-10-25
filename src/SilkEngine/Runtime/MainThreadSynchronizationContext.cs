using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

namespace SilkEngine.Runtime;

/// <summary>
/// Synchronization context bound to the main-loop thread: posted work is pumped once per frame and
/// <see cref="Send"/> blocks callers from other threads until the main thread runs the callback.
/// </summary>
internal class MainThreadSynchronizationContext : SynchronizationContext
{
    private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _queue = new();
    private readonly int _mainThreadId = Environment.CurrentManagedThreadId;
    public int MainThreadId => _mainThreadId;
    public bool IsMainThread => Environment.CurrentManagedThreadId == _mainThreadId;

    // await 恢复时会调用 Post（通常不带 ConfigureAwait(false) 时）
    public override void Post(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);
        _queue.Enqueue((d, state));
    }

    // 同步调度：非主线程调用会阻塞等待主线程 Pump 执行
    public override void Send(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);

        // 已经在主线程，直接内联执行，避免自己等自己造成死锁
        if (IsMainThread)
        {
            d(state);
            return;
        }

        using var done = new ManualResetEventSlim(false);
        Exception? error = null;

        _queue.Enqueue(
            (
                s =>
                {
                    try
                    {
                        d(s);
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                    }
                    finally
                    {
                        done.Set();
                    }
                },
                state
            )
        );

        done.Wait();

        if (error is not null)
            ExceptionDispatchInfo.Capture(error).Throw();
    }

    // 让上下文能传播到线程池线程：那边 await 后也会排回主线程队列
    public override SynchronizationContext CreateCopy() => this;

    /// <summary>主循环每帧调用，执行已排队的续体。</summary>
    internal void Pump(int maxPerFrame = int.MaxValue)
    {
        if (!IsMainThread)
            throw new InvalidOperationException("Pump 必须在主线程调用。");

        var processed = 0;
        while (processed < maxPerFrame && _queue.TryDequeue(out var item))
        {
            try
            {
                item.Callback(item.State);
            }
            catch (Exception ex)
            {
                SilkEngine.Log.Error(ex);
            }
            processed++;
        }
    }

    /// <summary>在主线程安装此上下文，返回实例供主循环持有。</summary>
    public static MainThreadSynchronizationContext Install()
    {
        var ctx = new MainThreadSynchronizationContext();
        SetSynchronizationContext(ctx);
        return ctx;
    }
}
