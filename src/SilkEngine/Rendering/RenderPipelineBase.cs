using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering;

/// <summary>
/// Rents one buffer per frame, records semantic commands, submits non-empty buffers, and returns
/// buffers on empty, recording-failure, and submit-failure paths.
/// </summary>
public abstract class RenderPipelineBase : IRenderPipeline
{
    private readonly object _gate = new();
    private IRenderBackend? _backend;
    private PipelineState _state;
    private bool _coreDisposed;

    /// <inheritdoc />
    public virtual string Name => GetType().Name;

    /// <summary>Gets the backend supplied to <see cref="Initialize"/>.</summary>
    protected IRenderBackend Backend =>
        _backend ?? throw new InvalidOperationException("Pipeline is not initialized.");

    /// <inheritdoc />
    public void Initialize(IRenderBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend);
        lock (_gate)
        {
            if (_state == PipelineState.Disposed)
                throw new ObjectDisposedException(GetType().Name);
            if (_state != PipelineState.Created)
                throw new InvalidOperationException("Pipeline has already been initialized.");

            _backend = backend;
            _state = PipelineState.Initializing;
            try
            {
                OnInitialize();
                _state = PipelineState.Initialized;
            }
            catch (Exception error)
            {
                _state = PipelineState.Faulted;
                try
                {
                    DisposeCoreOnce();
                }
                catch (Exception cleanupError)
                {
                    throw new AggregateException(
                        "Pipeline initialization and cleanup failed.",
                        error,
                        cleanupError
                    );
                }
                throw;
            }
        }
    }

    /// <inheritdoc />
    public void RenderFrame(RenderContext ctx)
    {
        lock (_gate)
        {
            EnsureInitialized();
            _state = PipelineState.Recording;
            try
            {
                var buffer = Backend.RentCommandBuffer();
                var submitted = false;
                try
                {
                    RecordCommands(buffer, ctx);
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
            catch
            {
                _state = PipelineState.Faulted;
                throw;
            }
            finally
            {
                if (_state == PipelineState.Recording)
                    _state = PipelineState.Initialized;
            }
        }
    }

    /// <inheritdoc />
    public void Resize(int width, int height)
    {
        lock (_gate)
        {
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
            catch
            {
                _state = PipelineState.Faulted;
                throw;
            }
            finally
            {
                if (_state == PipelineState.Resizing)
                    _state = PipelineState.Initialized;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            if (_state == PipelineState.Disposed)
                return;
            if (
                _state
                is PipelineState.Initializing
                    or PipelineState.Recording
                    or PipelineState.Resizing
            )
                throw new InvalidOperationException(
                    "Cannot dispose from an active pipeline callback."
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
    }

    /// <summary>Initializes pipeline-owned resources after the backend is assigned.</summary>
    protected abstract void OnInitialize();

    /// <summary>Records semantic commands for one frame.</summary>
    protected abstract void RecordCommands(ICommandBuffer commands, RenderContext ctx);

    /// <summary>Handles a drawable size change; default does nothing.</summary>
    protected virtual void OnResize(int width, int height) { }

    /// <summary>Releases pipeline resources; must tolerate partial initialization and runs at most once.</summary>
    protected virtual void DisposeCore() { }

    private void DisposeCoreOnce()
    {
        if (_coreDisposed)
            return;
        _coreDisposed = true;
        DisposeCore();
    }

    private void EnsureInitialized()
    {
        if (_state == PipelineState.Disposed)
            throw new ObjectDisposedException(GetType().Name);
        if (_state == PipelineState.Faulted)
            throw new InvalidOperationException("Pipeline is faulted.");
        if (_state != PipelineState.Initialized)
            throw new InvalidOperationException("Pipeline is not initialized.");
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
