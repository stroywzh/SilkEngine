using System;

namespace SilkEngine.Rendering;

/// <summary>
/// Shared lifecycle of the render backends and pipelines: owner-thread capture, the fault-cleanup
/// template, the display name and dispose-once.
/// </summary>
/// <remarks>
/// <para>
/// Every render-side object is single-threaded: the first <c>Initialize</c> of a derived type
/// captures the calling thread via <see cref="CaptureOwnerThread"/>, and every later call must come
/// from that thread (<see cref="EnsureOwner"/>). There is no locking on the render path.
/// </para>
/// <para>
/// <see cref="Dispose"/> stays abstract because each derived type owns its own state machine and
/// release ordering (the backend drains buffers, the pipeline drops its backend reference); both
/// must call <see cref="EnsureOwner"/> first and release their resources through
/// <see cref="DisposeCoreOnce"/> exactly once.
/// </para>
/// </remarks>
public abstract class RenderLifecycleBase : IDisposable
{
    private int _ownerThread;
    private bool _coreDisposed;

    /// <summary>Gets the display name used in diagnostics and exception messages.</summary>
    /// <remarks>Defaults to the runtime type name; derived types may override it with a friendlier name.</remarks>
    public virtual string Name => GetType().Name;

    /// <summary>
    /// Captures the calling thread as the owner. Call once, at the top of the derived
    /// <c>Initialize</c>, before any hook runs.
    /// </summary>
    protected void CaptureOwnerThread() => _ownerThread = Environment.CurrentManagedThreadId;

    /// <summary>Throws unless the caller is the thread that started initialization.</summary>
    /// <remarks>
    /// The check is skipped before the first initialization, so <see cref="Dispose"/> stays available
    /// for an object that was never initialized.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Called from a thread other than the owner.</exception>
    protected void EnsureOwner()
    {
        var owner = _ownerThread;
        if (owner == 0 || owner == Environment.CurrentManagedThreadId)
            return;

        throw new InvalidOperationException(
            $"{Name} is owned by thread {owner}; call it from that thread only."
        );
    }

    /// <summary>
    /// Runs an initialization body with the shared failure template: mark faulted, release derived
    /// resources once, then rethrow (wrapped when the cleanup itself fails too).
    /// </summary>
    /// <param name="onFaulted">Marks the derived state machine as faulted before cleanup runs.</param>
    /// <param name="core">Initialization body; a throw aborts initialization permanently.</param>
    protected void RunInitializeCore(Action onFaulted, Action core)
    {
        try
        {
            core();
        }
        catch (Exception error)
        {
            onFaulted();
            try
            {
                DisposeCoreOnce();
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException(
                    $"{Name} initialization and cleanup failed.",
                    error,
                    cleanupError
                );
            }
            throw;
        }
    }

    /// <summary>Releases the derived resources exactly once.</summary>
    /// <remarks>Safe to call from a failed initialization: it must tolerate partial state.</remarks>
    protected void DisposeCoreOnce()
    {
        if (_coreDisposed)
            return;
        _coreDisposed = true;
        OnDisposeCore();
    }

    /// <summary>Releases derived resources; must tolerate partial initialization and runs at most once.</summary>
    protected virtual void OnDisposeCore() { }

    /// <summary>Releases the object; derived types own their state machine, so each implements it.</summary>
    public abstract void Dispose();
}
