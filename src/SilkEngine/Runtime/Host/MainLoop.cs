using System.Diagnostics;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Runtime.Host;

/// <summary>Main-loop host that measures a complete logical frame including render/output waiting.</summary>
/// <remarks>Override frame hooks for application logic. All hooks run on the main-loop thread.
/// The host owns the supplied pipeline/backend; do not reuse them after start or dispose them externally.</remarks>
public sealed class MainLoop : RuntimeBase
{
    private readonly IRenderPipeline _pipeline;
    private readonly IRenderBackend _backend;
    private readonly Stopwatch _frameStopwatch = new();
    private RenderLoop? _renderLoop;
    private ulong _frameId;
    private long _lastFrameTicks;
    private int _lastFrameSucceeded = 1;

    /// <summary>Creates the main host using registrations frozen before application startup.</summary>
    /// <exception cref="ArgumentNullException">A required Application registration is missing.</exception>
    public MainLoop()
        : this(Application.RenderPipeline, Application.RenderBackend) { }

    /// <summary>Creates the main host with explicitly owned rendering dependencies.</summary>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    internal MainLoop(IRenderPipeline pipeline, IRenderBackend backend)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public override bool IsBackground => false;
    public override string ThreadName => "MainThread";
    internal override bool IsMainThread => true;

    /// <summary>Gets the full duration of the last completed frame; initially zero, safe for cross-thread reads.</summary>
    /// <remarks>A completed frame may still have failed to render; <see cref="RenderLoop"/> treats such a
    /// frame as consumed, and <c>LastFrameSucceeded</c> there reports whether its render pass succeeded.</remarks>
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
        var render = ThreadFactory.BuildRenderThread(_pipeline, _backend);
        Volatile.Write(ref _renderLoop, render);

        try
        {
            render.WaitUntilReady(StopToken);
        }
        catch (RenderLoopStoppedException)
        {
            RequestStop();
            StopToken.ThrowIfCancellationRequested();
        }
    }

    protected override void Run()
    {
        _frameStopwatch.Restart();
        var completed = false;
        try
        {
            OnFrameStarted();

            Update();
            StopToken.ThrowIfCancellationRequested();

            OnRendering();
            StopToken.ThrowIfCancellationRequested();

            OnFrameEnded();
            completed = true;

            // if (Application.LogSetting.EnableMainThreadLog)
            // {
            //     Log.Debug($"Main loop {Time.FrameCount} finished after {Time.UnscaledDeltaTime}");
            // }
        }
        catch (RenderLoopStoppedException)
        {
            RequestStop();
        }
        finally
        {
            _frameStopwatch.Stop();

            // The elapsed time was really spent even when the frame failed to render,
            // so every completed frame publishes it; success is tracked separately.
            if (completed)
            {
                Interlocked.Exchange(ref _lastFrameTicks, _frameStopwatch.Elapsed.Ticks);
            }

            if (!LastFrameSucceeded)
            {
                Log.Warning("Main Loop: Failed to render last frame.");
            }
        }
    }

    /// <summary>
    /// Called after frame timing starts; hook failures end the loop without publishing a successful duration.
    /// </summary>
    private void OnFrameStarted()
    {
        _frameId = checked(_frameId + 1);
        Time.BeginFrame(_frameId, LastFrameElapsed.TotalSeconds);
    }

    /// <summary>
    /// Called before start rendering this frame.
    /// </summary>
    private void Update() { }

    private void OnRendering()
    {
        var render = _renderLoop!;
        render.PublishFrame();
        render.WaitForFrameFinished(StopToken);
        Volatile.Write(ref _lastFrameSucceeded, render.LastFrameSucceeded ? 1 : 0);
    }

    /// <summary>
    /// Called after render/output completion and before the stopwatch stops; where process the end logics of this frame.
    /// </summary>
    private void OnFrameEnded() { }

    protected override void Shutdown()
    {
        var render = Volatile.Read(ref _renderLoop);
        if (render is null)
            return;
        render.RequestStop();
        render.Dispose();

        if (Application.LogSetting.EnableMainThreadLog)
        {
            Log.Debug($"Main loop Stopped at {Time.FrameCount}");
        }
    }

    // Called on the disposing thread: signal only. Main Shutdown owns child disposal/join.
    protected override void DisposeCore() => Volatile.Read(ref _renderLoop)?.RequestStop();
}
