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
/// backend supplies device initialization, command consumption, and optional GPU idling.
/// </summary>
public abstract class RenderBackendBase : IRenderBackend
{
    private readonly object _gate = new();
    private readonly Stack<CommandBufferStore> _freeStores = new();
    private readonly Queue<CommandBufferStore> _pending = new();
    private readonly HashSet<CommandBufferLease> _issued = new();
    private BackendState _state;
    private bool _coreDisposed;
    private bool _callbackActive;

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public void Initialize(IGraphicsDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        lock (_gate)
        {
            if (_state == BackendState.Disposed)
                throw new ObjectDisposedException(GetType().Name);
            if (_state == BackendState.Faulted)
                throw new InvalidOperationException("Backend is faulted.");
            if (_state != BackendState.Created)
                throw new InvalidOperationException("Backend has already been initialized.");

            _state = BackendState.Initializing;
            try
            {
                OnInitialize(device);
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
        }
        if (Application.LogSetting.EnableRenderLog)
        {
            Log.Debug($"Render Backend Initialized at {Time.FrameCount}");
        }
    }

    /// <inheritdoc />
    public ICommandBuffer RentCommandBuffer()
    {
        lock (_gate)
        {
            EnsureUsable();
            var store = _freeStores.Count > 0 ? _freeStores.Pop() : new CommandBufferStore();
            var lease = new CommandBufferLease(this, store);
            store.Lease = lease;
            _issued.Add(lease);
            return lease;
        }
    }

    /// <inheritdoc />
    public void RecycleCommandBuffer(ICommandBuffer buffer)
    {
        lock (_gate)
        {
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
    }

    /// <inheritdoc />
    public void Submit(ICommandBuffer buffer)
    {
        lock (_gate)
        {
            EnsureUsable();
            var lease = RequireOwnedLease(buffer);
            if (lease.State != LeaseState.Recording)
                throw new InvalidOperationException("Only a recording buffer can be submitted.");
            if (lease.Store.Commands.Count == 0)
                throw new ArgumentException(
                    "Cannot submit an empty command buffer.",
                    nameof(buffer)
                );

            // Validation complete; enqueue first, then make the transfer visible.
            _pending.Enqueue(lease.Store);
            lease.State = LeaseState.Submitted;
        }
    }

    /// <inheritdoc />
    public void ExecuteRender()
    {
        lock (_gate)
        {
            EnsureUsable();
            _callbackActive = true;
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
                _state = BackendState.Faulted;

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
                _callbackActive = false;
                if (Application.LogSetting.EnableRenderLog)
                {
                    Log.Debug("Render Backend finished RenderExecute().");
                }
            }
        }
    }

    /// <inheritdoc />
    public void WaitForIdle()
    {
        lock (_gate)
        {
            EnsureUsable();
            _callbackActive = true;
            try
            {
                OnWaitForIdle();
            }
            catch
            {
                _state = BackendState.Faulted;
                throw;
            }
            finally
            {
                _callbackActive = false;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            if (_state == BackendState.Disposed)
                return;
            if (_callbackActive || _state == BackendState.Initializing)
                throw new InvalidOperationException(
                    "Cannot dispose from an active backend callback."
                );
            _state = BackendState.Disposed;

            foreach (var lease in _issued)
            {
                lease.State = LeaseState.Disposed;
                lease.Store.Reset();
            }
            _issued.Clear();

            while (_pending.Count > 0)
                _pending.Dequeue().Reset();
            while (_freeStores.Count > 0)
                _freeStores.Pop().Reset();

            DisposeCoreOnce();
        }

        if (Application.LogSetting.EnableRenderLog)
        {
            Log.Debug("Render Backend disposed.");
        }
    }

    /// <summary>Initializes device state; throwing faults the backend and triggers one cleanup.</summary>
    protected abstract void OnInitialize(IGraphicsDevice device);

    /// <summary>Consumes one submitted buffer; the span is valid only during this call.</summary>
    /// <remarks>Copy/translate all CPU command data before returning or throwing. No asynchronous
    /// consumer may retain pointers to this span. GPU resource lifetimes remain the backend's
    /// responsibility and are independent from this CPU store's lifetime. Reentry is rejected.</remarks>
    protected abstract void ExecuteCommands(ReadOnlySpan<RenderCommand> commands);

    /// <summary>Backend-specific GPU synchronization; default does nothing.</summary>
    protected virtual void OnWaitForIdle() { }

    /// <summary>Releases device state; must tolerate partial initialization and runs at most once.</summary>
    protected virtual void OnDisposeCore() { }

    internal int GetCommandCount(CommandBufferLease lease)
    {
        lock (_gate)
        {
            EnsureUsable();
            if (lease.State != LeaseState.Recording)
                throw new InvalidOperationException("Command buffer is no longer writable.");
            return lease.Store.Commands.Count;
        }
    }

    internal void Record(CommandBufferLease lease, RenderCommand command)
    {
        lock (_gate)
        {
            EnsureUsable();
            if (lease.State != LeaseState.Recording)
                throw new InvalidOperationException("Command buffer is no longer writable.");
            lease.Store.Commands.Add(command);
        }
    }

    private void DisposeCoreOnce()
    {
        if (_coreDisposed)
            return;
        _coreDisposed = true;
        OnDisposeCore();
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
        if (_callbackActive)
            throw new InvalidOperationException("Backend callback reentry is not supported.");
        if (_state == BackendState.Faulted)
            throw new InvalidOperationException("Backend is faulted.");
        if (_state != BackendState.Initialized)
            throw new InvalidOperationException("Backend is not initialized.");
    }
}
