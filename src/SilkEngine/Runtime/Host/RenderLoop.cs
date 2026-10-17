using System.Runtime.ExceptionServices;
using SilkEngine.Rendering;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Runtime.Host;

internal sealed class RenderLoop : RuntimeBase
{
    private const int ConsecutiveFailureLimit = 120;
    internal static int MaxConsecutiveFailedFrames = ConsecutiveFailureLimit;

    private readonly IRenderPipeline _pipeline;
    private readonly IRenderBackend _backend;
    private readonly IGraphicsDevice _device;
    private readonly IRenderWindowHost? _host;
    private ulong _publishedFrameId;
    private ulong _completedFrameId;
    private ulong _failedFrameId;
    private int _ready;
    private int _stopping;
    private ExceptionDispatchInfo? _fault;
    private int _consecutiveFailures;
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

            _pipeline.Initialize(_backend);

            if (Volatile.Read(ref _stopping) == 0)
                Volatile.Write(ref _ready, 1);
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
    /// <remarks>Main thread only; it is the sole writer of <c>_publishedFrameId</c>.</remarks>
    internal void PublishFrame()
    {
        CheckAvailable(CancellationToken.None);

        if (Volatile.Read(ref _ready) == 0)
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

    /// <summary>Waits for the frame currently published in the single slot to finish.</summary>
    /// <exception cref="OperationCanceledException">The wait was canceled, the loop stopped, or it faulted.</exception>
    /// <remarks>Only the publishing thread calls this, so the slot holds exactly the frame to wait for.
    /// A stop published after this frame was consumed still counts as completion.</remarks>
    internal void WaitForFrameFinished(CancellationToken token)
    {
        var targetFrameId = Volatile.Read(ref _publishedFrameId);

        var wait = new SpinWait();
        while (Volatile.Read(ref _completedFrameId) < targetFrameId)
        {
            try
            {
                CheckAvailable(token);
            }
            catch (RenderLoopStoppedException)
            {
                // The loop can stop right after consuming the frame we wait for; that is still success.
                if (Volatile.Read(ref _completedFrameId) >= targetFrameId)
                    return;

                throw;
            }

            wait.SpinOnce();
        }
    }

    /// <summary>Gets whether the last completed frame finished without a failure.</summary>
    internal bool LastFrameSucceeded =>
        Volatile.Read(ref _failedFrameId) != Volatile.Read(ref _completedFrameId);

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
        Volatile.Write(ref _stopping, 1);
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

            //TODO: RenderContext 现在依旧是占位
            _pipeline.RenderFrame(default);
            _backend.ExecuteRender();
            _idleWait.Reset();

            // Render thread is the sole writer of _completedFrameId.
            if (Volatile.Read(ref _stopping) == 0 && Volatile.Read(ref _fault) is null)
                Volatile.Write(ref _completedFrameId, frameId);
            Volatile.Write(ref _consecutiveFailures, 0);
        }
        catch (OperationCanceledException) when (StopToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error) when (error is not ObjectDisposedException and not OperationCanceledException)
        {
            HandleFrameFailure(error);
        }
    }

    /// <summary>Records a failed frame as consumed, so the publisher can advance, then keeps running.</summary>
    private void HandleFrameFailure(Exception error)
    {
        Log.Exception(error);

        var frameId = Volatile.Read(ref _publishedFrameId);
        if (Volatile.Read(ref _stopping) != 0)
            return;

        Volatile.Write(ref _failedFrameId, frameId);
        Volatile.Write(ref _completedFrameId, frameId);

        var failures = Interlocked.Increment(ref _consecutiveFailures);
        if (failures < MaxConsecutiveFailedFrames)
            return;

        Log.Error($"Render loop stopped after {failures} consecutive failed frames.");
        RequestStop();
    }

    private void LatchFault(Exception error)
    {
        var existing = Volatile.Read(ref _fault);
        var failure = existing is null
            ? error
            : new AggregateException(
                "Rendering and cleanup failed.",
                existing.SourceException,
                error
            );

        Volatile.Write(ref _fault, ExceptionDispatchInfo.Capture(failure));
        Volatile.Write(ref _stopping, 1);
        base.RequestStop();
    }

    protected override void Shutdown()
    {
        Volatile.Write(ref _stopping, 1);

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
