using System;

namespace SilkEngine.Rendering;

/// <summary>Shared lifecycle for render backends and pipelines.</summary>
public abstract class RenderLifecycleBase : IDisposable
{
    private int _ownerThread;
    private bool _coreDisposed;

    /// <summary>Gets the display name used in diagnostics and exception messages.</summary>
    public virtual string Name => GetType().Name;

    /// <summary>Captures the calling thread as the owner.</summary>
    protected void CaptureOwnerThread() => _ownerThread = Environment.CurrentManagedThreadId;

    /// <summary>Throws when called from a thread other than the owner.</summary>
    protected void EnsureOwner()
    {
        var owner = _ownerThread;
        if (owner == 0 || owner == Environment.CurrentManagedThreadId)
            return;

        throw new InvalidOperationException(
            $"{Name} is owned by thread {owner}; call it from that thread only."
        );
    }

    /// <summary>Runs an initialization body with the shared failure template.</summary>
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
