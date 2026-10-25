using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering;

/// <summary>
/// Rents one buffer per frame, records semantic commands, submits non-empty buffers, and returns
/// buffers on empty, recording-failure, and submit-failure paths.
/// </summary>
/// <remarks>
/// Thread affinity: <see cref="Initialize"/> captures the calling thread as the owner. Every other
/// member must be called from that thread; calls from any other thread throw
/// <see cref="InvalidOperationException"/>. There is no locking on the render path.
/// </remarks>
public abstract class RenderPipelineBase : RenderLifecycleBase, IRenderPipeline
{
    private IRenderBackend? _backend;
    private PipelineState _state;

    /// <summary>Gets the backend supplied to <see cref="Initialize"/>.</summary>
    protected IRenderBackend Backend =>
        _backend ?? throw new InvalidOperationException($"{Name} is not initialized.");

    /// <inheritdoc />
    public void Initialize(IRenderBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend);
        EnsureOwner();

        if (_state == PipelineState.Disposed)
            throw new ObjectDisposedException(Name);
        if (_state == PipelineState.Faulted)
            throw new InvalidOperationException($"{Name} is faulted.");
        if (_state != PipelineState.Created)
            throw new InvalidOperationException($"{Name} has already been initialized.");

        _backend = backend;
        CaptureOwnerThread();
        _state = PipelineState.Initializing;
        RunInitializeCore(onFaulted: () => _state = PipelineState.Faulted, core: OnInitializeCore);
        _state = PipelineState.Initialized;
    }

    /// <inheritdoc />
    public void RecordFrame(in FrameContext context)
    {
        EnsureOwner();
        EnsureInitialized();

        _state = PipelineState.Recording;
        try
        {
            var buffer = Backend.RentCommandBuffer();
            var submitted = false;
            try
            {
                RecordCommands(buffer, context);
                if (buffer.CommandsCount > 0)
                {
                    Backend.Submit(buffer);
                    submitted = true;
                }
            }
            catch (Exception error)
            {
                try
                {
                    Backend.RecycleCommandBuffer(buffer);
                }
                catch (Exception cleanupError)
                {
                    throw new AggregateException(
                        "Recording/submission and buffer return failed.",
                        error,
                        cleanupError
                    );
                }
                throw;
            }

            if (!submitted)
            {
                Backend.RecycleCommandBuffer(buffer);
            }
        }
        finally
        {
            if (_state == PipelineState.Recording)
                _state = PipelineState.Initialized;
        }
    }

    /// <inheritdoc />
    public void Resize(int width, int height)
    {
        EnsureOwner();
        EnsureInitialized();

        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        if (width == 0 || height == 0)
            return;

        _state = PipelineState.Resizing;
        try
        {
            OnResize(width, height);
        }
        finally
        {
            if (_state == PipelineState.Resizing)
                _state = PipelineState.Initialized;
        }
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        EnsureOwner();

        if (_state == PipelineState.Disposed)
            return;
        if (
            _state
            is PipelineState.Initializing
                or PipelineState.Recording
                or PipelineState.Resizing
        )
            throw new InvalidOperationException(
                $"Cannot dispose {Name} from an active pipeline callback."
            );

        _state = PipelineState.Disposed;
        try
        {
            DisposeCoreOnce();
        }
        finally
        {
            _backend = null;
        }
    }

    /// <summary>Initializes pipeline-owned resources after the backend is assigned.</summary>
    protected abstract void OnInitializeCore();

    /// <summary>Records semantic commands for one frame.</summary>
    protected abstract void RecordCommands(ICommandBuffer commands, FrameContext context);

    /// <summary>Handles a drawable size change; default does nothing.</summary>
    protected virtual void OnResize(int width, int height) { }

    private void EnsureInitialized()
    {
        if (_state == PipelineState.Disposed)
            throw new ObjectDisposedException(Name);
        if (_state == PipelineState.Faulted)
            throw new InvalidOperationException($"{Name} is faulted.");
        if (_state != PipelineState.Initialized)
            throw new InvalidOperationException($"{Name} is not initialized.");
    }

    private enum PipelineState : byte
    {
        Created,
        Initializing,
        Initialized,
        Recording,
        Resizing,
        Faulted,
        Disposed,
    }
}
