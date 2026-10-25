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
/// Owns the backend state machine and its device; the command-buffer protocol itself lives in
/// <see cref="CommandQueue"/>. A derived backend creates the device; the device owns the
/// context, window events and presentation.
/// </summary>
/// <remarks>
/// <para>
/// Thread affinity: <see cref="Initialize"/> captures the calling thread as the owner. Every other
/// member, including the internal recording entry points, must be called from that thread; calls from
/// any other thread throw <see cref="InvalidOperationException"/>. There is no locking on the render
/// path, so violating the contract is detected, never silently serialized.
/// </para>
/// <para>
/// Recording is single-threaded: the recording entry points call <see cref="EnsureRecordThread"/>,
/// whose default requires the owner thread. A backend that records on a dedicated recorder thread 
/// overrides that hook; multi-threaded recording stays out of scope.
/// </para>
/// </remarks>
public abstract class RenderBackendBase : RenderLifecycleBase, IRenderBackend
{
    private readonly CommandQueue _queue;
    private BackendState _state;
    private IRenderDevice? _device;
    private bool _inCallback;

    protected RenderBackendBase()
    {
        _queue = new CommandQueue(this);
    }

    /// <inheritdoc />
    public IRenderDevice? Device => _device;

    /// <summary>Gets the device, throwing when the backend is not initialized.</summary>
    /// <exception cref="ObjectDisposedException">The backend has no device (disposed or not initialized).</exception>
    protected IRenderDevice RequireDevice() => _device ?? throw new ObjectDisposedException(Name);

    /// <inheritdoc />
    public void Initialize()
    {
        EnsureOwner();

        if (_state == BackendState.Disposed)
            throw new ObjectDisposedException(Name);
        if (_state == BackendState.Faulted)
            throw new InvalidOperationException($"{Name} is faulted.");
        if (_state != BackendState.Created)
            throw new InvalidOperationException($"{Name} has already been initialized.");

        CaptureOwnerThread();
        _state = BackendState.Initializing;
        RunInitializeCore(
            onFaulted: () => _state = BackendState.Faulted,
            core: () => _device = CreateDevice()
        );
        _state = BackendState.Initialized;

        if (Application.LogSetting.EnableRenderLog)
        {
            Log.Debug($"Render backend initialized at {Time.FrameIndex}");
        }
    }

    /// <inheritdoc />
    public ICommandBuffer RentCommandBuffer()
    {
        EnsureOwner();
        EnsureRecordThread();
        EnsureUsable();

        return _queue.Rent();
    }

    /// <inheritdoc />
    public void RecycleCommandBuffer(ICommandBuffer buffer)
    {
        EnsureOwner();
        EnsureUsable();

        _queue.Recycle(buffer);
    }

    /// <inheritdoc />
    public void Submit(ICommandBuffer buffer)
    {
        EnsureOwner();
        EnsureRecordThread();
        EnsureUsable();

        _queue.Submit(buffer);
    }

    /// <inheritdoc />
    public void ExecuteFrame(in FrameContext frameContext)
    {
        EnsureOwner();
        EnsureRecordThread();
        EnsureUsable();

        if (frameContext.FrameIndex == 0)
        {
            // the snapshot is the backend's contract (frame-accurate diagnostics); a frame
            // without a published index means the host skipped Time.BeginFrame.
            throw new InvalidOperationException(
                "ExecuteFrame requires a published frame snapshot (FrameIndex is zero)."
            );
        }

        var device = RequireDevice();

        _inCallback = true;
        try
        {
            _queue.Execute(device, in frameContext);
        }
        finally
        {
            _inCallback = false;
        }
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        EnsureOwner();

        if (_state == BackendState.Disposed)
            return;
        if (_inCallback || _state == BackendState.Initializing)
            throw new InvalidOperationException($"Cannot dispose {Name} from an active callback.");

        _state = BackendState.Disposed;

        _queue.Clear();

        DisposeCoreOnce();

        if (Application.LogSetting.EnableRenderLog)
        {
            Log.Debug("Render backend disposed.");
        }
    }

    /// <summary>Creates the device (context, window host and presentation) exactly once.</summary>
    /// <returns>The device owned by this backend.</returns>
    protected abstract IRenderDevice CreateDevice();

    /// <summary>
    /// Dispatches one recorded command through this assembly's command table.
    /// </summary>
    /// <param name="id">Recorded command id.</param>
    /// <param name="payload">Raw bytes of the command struct.</param>
    /// <param name="context">Execution context.</param>
    /// <returns><c>true</c> when an executor ran; <c>false</c> when the id is unknown to this assembly.</returns>
    /// <remarks>
    /// The engine assembly table is used by default. A backend that declares its own commands overrides
    /// this to try its own table first and then fall back to <c>base.Dispatch</c>.
    /// </remarks>
    protected virtual bool Dispatch(byte id, ReadOnlySpan<byte> payload, ref CommandContext ctx) =>
        RenderCommandTable.TryDispatch(id, payload, ref ctx);

    /// <summary>
    /// Verifies that the calling thread may record or execute commands (single-thread recording).
    /// </summary>
    /// <remarks>
    /// Default: the recording thread must be the backend's owner thread. The hook exists so a backend
    /// that owns a dedicated recorder thread can override it without changing the public surface; the
    /// command queue itself never performs thread checks.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The caller is not the recording thread.</exception>
    protected virtual void EnsureRecordThread() => EnsureOwner();

    /// <summary>Bridge used by <see cref="CommandQueue"/> to reach the virtual dispatch table.</summary>
    internal bool DispatchCommand(byte id, ReadOnlySpan<byte> payload, ref CommandContext ctx) =>
        Dispatch(id, payload, ref ctx);

    /// <summary>Disposes the device (context, window, GL objects) exactly once.</summary>
    protected override void OnDisposeCore()
    {
        var device = _device;
        _device = null;
        device?.Dispose();
    }

    internal int GetCommandCount(CommandBufferLease lease)
    {
        EnsureOwner();
        EnsureUsable();
        if (lease.State != LeaseState.Recording)
            throw new InvalidOperationException("Command buffer is no longer writable.");
        return lease.Store.Entries.Count;
    }

    internal void Record(CommandBufferLease lease, int commandId, ReadOnlySpan<byte> payload)
    {
        EnsureOwner();
        EnsureRecordThread();
        EnsureUsable();

        if (lease.State != LeaseState.Recording)
            throw new InvalidOperationException("Command buffer is no longer writable.");
        if ((uint)commandId > byte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(commandId));

        lease.Store.Append((byte)commandId, payload);
    }

    private void EnsureUsable()
    {
        if (_state == BackendState.Disposed)
            throw new ObjectDisposedException(Name);
        if (_inCallback)
            throw new InvalidOperationException("Backend callback reentry is not supported.");
        if (_state == BackendState.Faulted)
            throw new InvalidOperationException($"{Name} is faulted.");
        if (_state != BackendState.Initialized)
            throw new InvalidOperationException($"{Name} is not initialized.");
    }
}
