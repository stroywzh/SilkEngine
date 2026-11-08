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
    private static readonly int _mainThreadId = Environment.CurrentManagedThreadId;
    public static int MainThreadId => _mainThreadId;
    public bool IsMainThread => Environment.CurrentManagedThreadId == _mainThreadId;

    // Post is called when an await resumes (without ConfigureAwait(false) by default).
    public override void Post(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);
        _queue.Enqueue((d, state));
    }

    // Synchronous dispatch: a non-main-thread call blocks until the main thread pumps it.
    public override void Send(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);

        // Already on the main thread: execute inline to avoid self-deadlock.
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

    // Let the context flow into thread-pool threads: their awaits also queue back to the main thread.
    public override SynchronizationContext CreateCopy() => this;

    /// <summary>Called by the main loop each frame to execute queued continuations.</summary>
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

    /// <summary>Installs this context on the main thread; returns the instance for the main loop to hold.</summary>
    public static MainThreadSynchronizationContext Install()
    {
        var ctx = new MainThreadSynchronizationContext();
        SetSynchronizationContext(ctx);
        return ctx;
    }
}
