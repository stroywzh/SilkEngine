using System.Diagnostics;
using SilkEngine.Core.AssetSystem;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Runtime.Host;

/// <summary>Main-loop host that measures a complete logical frame including render/output waiting.</summary>
/// <remarks>All hooks run on the main-loop thread.
/// The host owns the supplied pipeline/backend; do not reuse them after start or dispose them externally.</remarks>
public sealed class MainLoop : EngineThread
{
    private static readonly MainThreadSynchronizationContext _mainThreadContext =
        MainThreadSynchronizationContext.Install();

    // TODO: decouple; do not hardcode.
    internal const double FixedStepSeconds = 1.0 / 60.0;

    private const int MaxFixedStepsPerFrame = 8;

    private const int MaxConsecutiveFailedFrames = 120;

    private readonly IRenderPipeline _pipeline;
    private readonly IRenderBackend _backend;
    private readonly Stopwatch _frameStopwatch = new();
    private RenderThread? _renderThread;
    private AssetThread? _assetThread;
    private ulong _frameId;
    private long _lastFrameTicks;
    private int _lastFrameSucceeded = 1;
    private int _consecutiveFailures;
    private double _fixedAccumulator;

    /// <summary>Creates the main host using registrations frozen before application startup.</summary>
    public MainLoop()
        : this(Application.RenderPipeline, Application.RenderBackend) { }

    internal MainLoop(IRenderPipeline pipeline, IRenderBackend backend)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }
    public override bool IsBackground => false;
    public override string ThreadName => "MainThread";
    internal override bool IsMainThread => true;

    /// <summary>Gets the full duration of the last completed frame; initially zero, safe for cross-thread reads.</summary>
    /// <remarks>A completed frame may still have failed to render: its duration is published anyway, and
    /// <see cref="LastFrameSucceeded"/> reports whether the render pass succeeded.</remarks>
    public TimeSpan LastFrameElapsed => TimeSpan.FromTicks(Volatile.Read(ref _lastFrameTicks));

    /// <summary>
    /// Gets whether the last completed frame's render pass succeeded; initially true, safe for
    /// cross-thread reads.
    /// </summary>
    /// <remarks>Failures are non-fatal: a false value describes that one frame only and the loop keeps
    /// running. The frame's duration is still published through <see cref="LastFrameElapsed"/>.</remarks>
    public bool LastFrameSucceeded => Volatile.Read(ref _lastFrameSucceeded) != 0;

    protected override void Initialize()
    {
        Time.Reset();

        // The asset worker starts first so requests submitted before startup begin loading immediately.
        var assets = ThreadFactory.StartAssetThread();
        Volatile.Write(ref _assetThread, assets);

        var render = ThreadFactory.StartRenderThread(_pipeline, _backend);
        Volatile.Write(ref _renderThread, render);

        try
        {
            render.WaitUntilReady(StopToken);
        }
        catch (RenderThreadStoppedException)
        {
            RequestStop();
            StopToken.ThrowIfCancellationRequested();
        }
    }

    private enum FrameStage
    {
        PreStart = 0,
        Starting = 1,
        Rendered = 2,
        Ending = 3,
    }

    protected override void Run()
    {
        _frameStopwatch.Restart();

        var stage = FrameStage.PreStart;
        var publishElapsed = false;

        try
        {
            OnFrameStarted();
            stage = FrameStage.Starting;

            UpdateScheduler.RefreshSnapshot();

            DispatchFixedUpdates();
            StopToken.ThrowIfCancellationRequested();

            UpdateScheduler.DispatchUpdate();
            StopToken.ThrowIfCancellationRequested();

            OnRendering();
            stage = FrameStage.Rendered;
            StopToken.ThrowIfCancellationRequested();

            stage = FrameStage.Ending;
            OnFrameEnded();
            publishElapsed = true;

            Interlocked.Exchange(ref _consecutiveFailures, 0);
        }
        catch (RenderThreadStoppedException)
        {
            RequestStop();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception error)
        {
            publishElapsed = HandleFrameFailure(error, stage);
        }
        finally
        {
            _frameStopwatch.Stop();

            if (publishElapsed)
            {
                Interlocked.Exchange(ref _lastFrameTicks, _frameStopwatch.Elapsed.Ticks);
            }

            if (!LastFrameSucceeded)
            {
                Log.Warning("Main Loop: Failed to render last frame.");
            }
        }
    }

    private void OnFrameStarted()
    {
        _frameId = checked(_frameId + 1);
        Time.BeginFrame(_frameId, LastFrameElapsed.TotalSeconds);
    }

    private void DispatchFixedUpdates()
    {
        _fixedAccumulator += Time.DeltaSeconds;

        var maxAccumulated = FixedStepSeconds * MaxFixedStepsPerFrame;
        if (_fixedAccumulator > maxAccumulated)
        {
            _fixedAccumulator = maxAccumulated;
        }

        while (_fixedAccumulator >= FixedStepSeconds)
        {
            _fixedAccumulator -= FixedStepSeconds;
            UpdateScheduler.DispatchFixedUpdate();
        }
    }

    private void OnRendering()
    {
        var render = _renderThread!;
        render.PublishFrame();
        render.WaitForFrameFinished(StopToken);
        Volatile.Write(ref _lastFrameSucceeded, render.LastFrameSucceeded ? 1 : 0);
    }

    private void OnFrameEnded()
    {
        UpdateScheduler.DispatchLateUpdate();

        // Asset completions run on the main thread; the budget keeps the frame bounded.
        AssetSystem.DrainCompletions(Application.AssetSetting.CompletionPumpBudget);
        _mainThreadContext.Pump();
    }

    private bool HandleFrameFailure(Exception error, FrameStage stage)
    {
        Log.Exception(error);

        var failures = Interlocked.Increment(ref _consecutiveFailures);

        if (stage == FrameStage.PreStart)
        {
            Log.Error("Main loop stopped: the frame failed before its id was announced.");
            RequestStop();
            return false;
        }

        if (stage != FrameStage.Ending)
        {
            if (stage == FrameStage.Starting && !TryRenderFailedFrame())
            {
                Log.Error("Main loop stopped: the failed frame could not finish on the render thread.");
                RequestStop();
                return false;
            }

            TryEndFailedFrame();
        }

        if (failures >= MaxConsecutiveFailedFrames)
        {
            Log.Error($"Main loop stopped after {failures} consecutive failed frames.");
            RequestStop();
        }

        return true;
    }

    private bool TryRenderFailedFrame()
    {
        try
        {
            OnRendering();
            return true;
        }
        catch (RenderThreadStoppedException)
        {
            RequestStop();
            return false;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Log.Exception(error);
            return false;
        }
    }

    private void TryEndFailedFrame()
    {
        try
        {
            OnFrameEnded();
        }
        catch (RenderThreadStoppedException)
        {
            RequestStop();
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Log.Exception(error);
        }
    }

    protected override void Shutdown()
    {
        // Render first: GPU consumers stop before the asset side stops publishing ready/release ids.
        var render = Volatile.Read(ref _renderThread);
        render?.RequestStop();
        render?.Dispose();

        var assets = Volatile.Read(ref _assetThread);
        assets?.RequestStop();
        assets?.Dispose();

        // The worker completes pending requests while stopping; pump them so no caller keeps waiting.
        AssetSystem.DrainCompletions(int.MaxValue);

        if (Application.LogSetting.EnableMainThreadLog)
        {
            Log.Debug($"Main loop Stopped at {Time.FrameIndex}");
        }
    }

    protected override void DisposeCore()
    {
        Volatile.Read(ref _renderThread)?.RequestStop();
        Volatile.Read(ref _assetThread)?.RequestStop();
    }
}
