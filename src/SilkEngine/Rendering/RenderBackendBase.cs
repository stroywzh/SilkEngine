using System.Numerics;
using System.Runtime.InteropServices;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering;

internal enum BackendState : byte
{
    Created,
    Initializing,
    Initialized,
    Faulted,
    Disposed,
}

/// <summary>
/// Owns command-buffer storage and implements the CPU record/submit/execute protocol. A derived
/// backend supplies device initialization, command consumption, and presentation.
/// </summary>
/// <remarks>
/// Thread affinity: <see cref="Initialize"/> captures the calling thread as the owner. Every other
/// member, including the internal recording entry points, must be called from that thread; calls
/// from any other thread throw <see cref="InvalidOperationException"/>. There is no locking on the
/// render path, so violating the contract is detected, never silently serialized.
/// </remarks>
public abstract class RenderBackendBase : IRenderBackend
{
    private readonly Stack<CommandBufferStore> _freeStores = new();
    private readonly Queue<CommandBufferStore> _pending = new();
    private readonly HashSet<CommandBufferLease> _issued = new();
    private BackendState _state;
    private IRenderHardware _hardware;
    private bool _coreDisposed;
    private bool _inCallback;
    private int _ownerThread;

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <summary>Gets the thread that owns this backend, or zero before <see cref="Initialize"/>.</summary>
    protected int OwnerThread => _ownerThread;

    /// <summary>
    /// RHI
    /// </summary>
    protected IRenderHardware Hardware =>
        _hardware ?? throw new ObjectDisposedException(GetType().Name);

    /// <inheritdoc />
    public void Initialize()
    {
        EnsureOwner();

        if (_state == BackendState.Disposed)
            throw new ObjectDisposedException(GetType().Name);
        if (_state == BackendState.Faulted)
            throw new InvalidOperationException("Backend is faulted.");
        if (_state != BackendState.Created)
            throw new InvalidOperationException("Backend has already been initialized.");

        _ownerThread = Environment.CurrentManagedThreadId;
        _state = BackendState.Initializing;
        try
        {
            OnInitialize();
            _hardware = CreateHardware();
            _state = BackendState.Initialized;
        }
        catch (Exception error)
        {
            _state = BackendState.Faulted;
            try
            {
                DisposeCoreOnce();
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException(
                    "Backend initialization and cleanup failed.",
                    error,
                    cleanupError
                );
            }
            throw;
        }

        if (Application.LogSetting.EnableRenderLog)
        {
            Log.Debug($"Render Backend Initialized at {Time.FrameCount}");
        }
    }

    /// <inheritdoc />
    public ICommandBuffer RentCommandBuffer()
    {
        EnsureOwner();
        EnsureUsable();

        var store = _freeStores.Count > 0 ? _freeStores.Pop() : new CommandBufferStore();
        var lease = new CommandBufferLease(this, store);
        store.Lease = lease;
        _issued.Add(lease);
        return lease;
    }

    /// <inheritdoc />
    public void RecycleCommandBuffer(ICommandBuffer buffer)
    {
        EnsureOwner();
        EnsureUsable();

        var lease = RequireOwnedLease(buffer);
        if (lease.State != LeaseState.Recording)
            throw new InvalidOperationException("Only a recording buffer can be returned.");

        lease.State = LeaseState.Returned;
        _issued.Remove(lease);
        var store = lease.Store;
        store.Reset();
        _freeStores.Push(store);
    }

    /// <inheritdoc />
    public void Submit(ICommandBuffer buffer)
    {
        EnsureOwner();
        EnsureUsable();

        var lease = RequireOwnedLease(buffer);
        if (lease.State != LeaseState.Recording)
            throw new InvalidOperationException("Only a recording buffer can be submitted.");

        if (lease.Store.Commands.Count == 0)
        {
            Log.Warning("You have submited an empty buffer.");
        }

        // Validation complete; enqueue first, then make the transfer visible.
        _pending.Enqueue(lease.Store);
        lease.State = LeaseState.Submitted;
    }

    /// <inheritdoc />
    public void ExecuteRender()
    {
        EnsureOwner();
        EnsureUsable();

        _inCallback = true;
        try
        {
            while (_pending.Count > 0)
            {
                var store = _pending.Peek();
                ExecuteCommands(CollectionsMarshal.AsSpan(store.Commands));

                _pending.Dequeue();
                var lease = store.Lease!;
                lease.State = LeaseState.Executed;
                _issued.Remove(lease);
                store.Reset();
                _freeStores.Push(store);
            }
        }
        catch
        {
            // One frame failed: drop everything still queued, keep the backend usable.
            // Do not replay executed buffers; do not leave pending buffers writable.
            while (_pending.Count > 0)
            {
                var store = _pending.Dequeue();
                var lease = store.Lease!;
                lease.State = LeaseState.Faulted;
                _issued.Remove(lease);
                store.Reset();
                _freeStores.Push(store);
            }
            throw;
        }
        finally
        {
            _inCallback = false;
            // if (Application.LogSetting.EnableRenderLog)
            // {
            //     Log.Debug("Render Backend finished RenderExecute().");
            // }
        }

        OnPresent();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        EnsureOwner();

        if (_state == BackendState.Disposed)
            return;
        if (_inCallback || _state == BackendState.Initializing)
            throw new InvalidOperationException("Cannot dispose from an active backend callback.");

        _state = BackendState.Disposed;

        foreach (var lease in _issued)
        {
            lease.State = LeaseState.Disposed;
            lease.Store.Reset();
        }
        _issued.Clear();

        while (_pending.Count > 0)
        {
            _pending.Dequeue().Reset();
        }

        while (_freeStores.Count > 0)
        {
            _freeStores.Pop().Reset();
        }

        DisposeCoreOnce();

        if (Application.LogSetting.EnableRenderLog)
        {
            Log.Debug("Render Backend disposed.");
        }
    }

    /// <summary>Initializes device state; throwing faults the backend and triggers one cleanup.</summary>
    protected abstract void OnInitialize();

    /// <summary>
    /// A RHI layer hook,called after <see cref="OnInitialize"/>
    /// </summary>
    /// <returns></returns>
    protected abstract IRenderHardware CreateHardware();

    /// <summary>Consumes one submitted buffer; the span is valid only during this call.</summary>
    /// <remarks>Copy/translate all CPU command data before returning or throwing. No asynchronous
    /// consumer may retain pointers to this span. GPU resource lifetimes remain the backend's
    /// responsibility and are independent from this CPU store's lifetime. Reentry is rejected.</remarks>
    protected abstract void ExecuteCommands(ReadOnlySpan<RenderCommand> commands);

    /// <summary>Presents the frame after all pending buffers were consumed; default does nothing.</summary>
    /// <remarks>Runs once per <see cref="ExecuteRender"/> on the backend's owning thread, outside any
    /// command iteration. A throw is a single-frame failure: the backend stays usable.</remarks>
    protected virtual void OnPresent() { }

    /// <summary>Releases device state; must tolerate partial initialization and runs at most once.</summary>
    protected virtual void OnDisposeCore() { }

    /// <summary>Throws unless the caller is the thread that initialized this backend.</summary>
    /// <remarks>Skips the check before the first <see cref="Initialize"/>, so <see cref="Dispose"/>
    /// stays available for an object that was never initialized.</remarks>
    /// <exception cref="InvalidOperationException">Called from a thread other than the owner.</exception>
    protected void EnsureOwner()
    {
        var owner = _ownerThread;
        if (owner == 0 || owner == Environment.CurrentManagedThreadId)
            return;

        throw new InvalidOperationException(
            $"Backend is owned by thread {owner}; call it from that thread only."
        );
    }

    internal int GetCommandCount(CommandBufferLease lease)
    {
        EnsureOwner();
        EnsureUsable();
        if (lease.State != LeaseState.Recording)
            throw new InvalidOperationException("Command buffer is no longer writable.");
        return lease.Store.Commands.Count;
    }

    internal void Record(CommandBufferLease lease, RenderCommand command)
    {
        EnsureOwner();
        EnsureUsable();
        if (lease.State != LeaseState.Recording)
            throw new InvalidOperationException("Command buffer is no longer writable.");
        lease.Store.Commands.Add(command);
    }

    private void DisposeCoreOnce()
    {
        if (_coreDisposed)
            return;
        _coreDisposed = true;

        OnDisposeCore();
        _hardware = null!;
    }

    private CommandBufferLease RequireOwnedLease(ICommandBuffer? buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (buffer is not CommandBufferLease lease || !ReferenceEquals(lease.Owner, this))
            throw new ArgumentException(
                "The command buffer was not rented from this backend.",
                nameof(buffer)
            );
        return lease;
    }

    private void EnsureUsable()
    {
        if (_state == BackendState.Disposed)
            throw new ObjectDisposedException(GetType().Name);
        if (_inCallback)
            throw new InvalidOperationException("Backend callback reentry is not supported.");
        if (_state == BackendState.Faulted)
            throw new InvalidOperationException("Backend is faulted.");
        if (_state != BackendState.Initialized)
            throw new InvalidOperationException("Backend is not initialized.");
    }
}
