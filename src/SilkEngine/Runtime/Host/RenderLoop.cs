using System.Runtime.ExceptionServices;
using SilkEngine.Rendering;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Runtime.Host;

internal sealed class RenderLoop : RuntimeBase
{
    private readonly object _frameGate = new();
    private readonly IRenderPipeline _pipeline;
    private readonly IRenderBackend _backend;
    private readonly IGraphicsDevice _device;
    private readonly IRenderWindowHost? _host;
    private ulong _publishedFrameId;
    private ulong _completedFrameId;
    private int _ready;
    private int _stopping;
    private ExceptionDispatchInfo? _fault;
    private bool _backendInitialized;
    private SpinWait _idleWait;

    public RenderLoop()
        : this(Application.RenderPipeline, Application.RenderBackend, Application.GraphicsDevice)
    { }

    internal RenderLoop(IRenderPipeline pipeline, IRenderBackend backend, IGraphicsDevice device)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _host = backend as IRenderWindowHost;
    }

    public override string ThreadName => "RenderThread";

    protected override void Initialize()
    {
        try
        {
            StopToken.ThrowIfCancellationRequested();

            _backend.Initialize(_device);
            _backendInitialized = true;

            _pipeline.Initialize(_backend);

            lock (_frameGate)
            {
                if (_stopping == 0)
                    Volatile.Write(ref _ready, 1);
            }
        }
        catch (OperationCanceledException) when (StopToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            LatchFault(error);
            throw;
        }

        if (Application.LogSetting.EnableRenderLog)
        {
            Log.Debug("Render Loop initialized.");
        }
    }

    internal void WaitUntilReady(CancellationToken token)
    {
        var wait = new SpinWait();
        while (true)
        {
            CheckAvailable(token);
            if (Volatile.Read(ref _ready) != 0)
                return;
            wait.SpinOnce();
        }
    }

    /// <summary>Publishes the frame announced by <see cref="Time"/> into the single slot.</summary>
    /// <exception cref="InvalidOperationException">The loop is not ready or a frame is still in flight.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The announced id is not the next consecutive id.</exception>
    /// <remarks>The announced values are snapshotted here, so the consumer never re-reads the global clock.</remarks>
    internal void PublishFrame()
    {
        lock (_frameGate)
        {
            CheckAvailable(CancellationToken.None);

            if (_ready == 0)
                throw new InvalidOperationException("Render loop is not ready.");

            var published = Volatile.Read(ref _publishedFrameId);
            if (published != Volatile.Read(ref _completedFrameId))
                throw new InvalidOperationException("A frame is still in flight.");

            var frameId = Time.FrameCount;
            if (frameId != checked(published + 1))
                throw new ArgumentOutOfRangeException(
                    nameof(frameId),
                    "Frame ids must be consecutive from one."
                );

            Volatile.Write(ref _publishedFrameId, frameId);
        }
    }

    /// <summary>Waits for the frame currently published in the single slot to finish.</summary>
    /// <exception cref="OperationCanceledException">The wait was canceled, the loop stopped, or it faulted.</exception>
    /// <remarks>Only the publishing thread calls this, so the slot holds exactly the frame to wait for.</remarks>
    internal void WaitForFrameFinished(CancellationToken token)
    {
        var targetFrameId = Volatile.Read(ref _publishedFrameId);

        var wait = new SpinWait();
        while (Volatile.Read(ref _completedFrameId) < targetFrameId)
        {
            CheckAvailable(token);
            wait.SpinOnce();
        }
    }

    private void CheckAvailable(CancellationToken token)
    {
        Volatile.Read(ref _fault)?.Throw();

        token.ThrowIfCancellationRequested();

        if (Volatile.Read(ref _stopping) != 0)
        {
            // A fault may have been published between the first fault read and the stop read.
            Volatile.Read(ref _fault)?.Throw();
            throw new RenderLoopStoppedException();
        }
    }

    public override void RequestStop()
    {
        lock (_frameGate)
        {
            Volatile.Write(ref _stopping, 1);
        }

        base.RequestStop();
    }

    protected override void Run()
    {
        try
        {
            _host?.PumpEvents();
            if (_host?.IsClosing == true)
            {
                RequestStop();
                return;
            }

            if (Volatile.Read(ref _stopping) != 0)
                return;

            var frameId = Volatile.Read(ref _publishedFrameId);
            if (frameId == Volatile.Read(ref _completedFrameId))
            {
                _idleWait.SpinOnce();
                return;
            }

            var invoked = false;

            void Execute()
            {
                if (invoked)
                    throw new InvalidOperationException("Frame callback was invoked twice.");
                invoked = true;
                //TODO: RenderContext 现在依旧是占位
                _pipeline.RenderFrame(default);
                _backend.ExecuteRender();
            }

            bool rendered;
            if (_host is null)
            {
                Execute();
                rendered = true;
            }
            else
            {
                rendered = _host.TryRenderFrame(Execute);
            }

            if (rendered != invoked)
                throw new InvalidOperationException(
                    "Window host violated its render callback contract."
                );

            if (!rendered)
            {
                _idleWait.SpinOnce();
                return;
            }
            _idleWait.Reset();

            lock (_frameGate)
            {
                if (_stopping == 0 && _fault is null)
                    Volatile.Write(ref _completedFrameId, frameId);
            }
        }
        catch (OperationCanceledException) when (StopToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            LatchFault(error);
            throw;
        }
    }

    private void LatchFault(Exception error)
    {
        lock (_frameGate)
        {
            var failure = _fault is null
                ? error
                : new AggregateException(
                    "Rendering and cleanup failed.",
                    _fault.SourceException,
                    error
                );

            Volatile.Write(ref _fault, ExceptionDispatchInfo.Capture(failure));
            Volatile.Write(ref _stopping, 1);
        }
        base.RequestStop();
    }

    protected override void Shutdown()
    {
        lock (_frameGate)
        {
            Volatile.Write(ref _stopping, 1);
        }

        var errors = new List<Exception>();
        void Cleanup(Action action)
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                errors.Add(error);
            }
        }

        if (_backendInitialized && Volatile.Read(ref _fault) is null)
        {
            Cleanup(_backend.WaitForIdle);
        }

        Cleanup(_pipeline.Dispose);
        Cleanup(_backend.Dispose);

        if (errors.Count > 0)
        {
            var error = new AggregateException("Render shutdown failed.", errors);
            LatchFault(error);
            throw error;
        }
    }
}

internal sealed class RenderLoopStoppedException : OperationCanceledException
{
    internal RenderLoopStoppedException()
        : base("Render loop stopped before the requested operation completed.") { }
}
