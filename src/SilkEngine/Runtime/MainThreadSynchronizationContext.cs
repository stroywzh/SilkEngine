using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

namespace SilkEngine.Runtime;

/// <summary>
/// Synchronization context bound to the main-loop thread: posted work is pumped once per frame and
/// <see cref="Send"/> blocks callers from other threads until the main thread runs the callback.
/// </summary>
/// <remarks>The context lives exactly as long as the main loop: <see cref="Install"/> creates it,
/// <see cref="Shutdown"/> drains what is still queued on the loop thread, and <see cref="Uninstall"/>
/// restores the previous ambient context. Work posted after shutdown is dropped instead of being queued
/// into a context that will never be pumped again.</remarks>
internal class MainThreadSynchronizationContext : SynchronizationContext
{
    private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _queue = new();
    private readonly int _mainThreadId;
    private int _closed;

    private MainThreadSynchronizationContext(int threadId)
    {
        _mainThreadId = threadId;
    }
    public int MainThreadId => _mainThreadId;
    public bool IsMainThread => Environment.CurrentManagedThreadId == _mainThreadId;

    // Post is called when an await resumes (without ConfigureAwait(false) by default).
    public override void Post(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);

        if (Volatile.Read(ref _closed) != 0)
        {
            // The loop is gone: queueing would leak the callback forever, and throwing would fault the
            // await that is already unwinding the engine.
            SilkEngine.Log.Warning(
                "Main-thread sync context is closed; the posted callback was dropped."
            );
            return;
        }

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

        if (Volatile.Read(ref _closed) != 0)
        {
            throw new InvalidOperationException(
                "The main loop has stopped; synchronous dispatch is no longer available."
            );
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
            throw new InvalidOperationException(
                $"Pump 必须在主线程调用。(Main={MainThreadId}, Current={Environment.CurrentManagedThreadId}, CurrentName={Thread.CurrentThread.Name})"
            );

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

    /// <summary>Stops accepting work and drains callbacks already queued; runs on the main-loop thread.</summary>
    internal void Shutdown()
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
            return;

        // Callers blocked in Send must be released before the loop thread exits.
        Pump(int.MaxValue);
    }

    /// <summary>Restores the ambient context when this instance is still installed; safe to call with null.</summary>
    internal static void Uninstall(MainThreadSynchronizationContext? context)
    {
        if (context is not null && ReferenceEquals(Current, context))
            SetSynchronizationContext(null);
    }

    /// <summary>Installs this context on the main thread; returns the instance for the main loop to hold.</summary>
    public static MainThreadSynchronizationContext Install()
    {
        var ctx = new MainThreadSynchronizationContext(Environment.CurrentManagedThreadId);
        SetSynchronizationContext(ctx);
        return ctx;
    }
}
