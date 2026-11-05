using System.Collections.Concurrent;
using System.Diagnostics;
using SilkEngine.Asset;
using SilkEngine.Core.AssetSystem;

namespace SilkEngine.Runtime.Host;

/// <summary>Engine-level worker: consumes asset requests and dispatches decoding to the thread pool.</summary>
/// <remarks>This thread owns <see cref="AssetStore"/>: registration, snapshot publishing, refcounting and the
/// release window all happen here, so the single-writer rule holds. Decoding runs on thread-pool tasks and their
/// results come back through a queue drained by this thread. The static request queue is consumed only after this
/// thread starts, so requests submitted earlier simply wait.</remarks>
internal sealed class AssetThread : EngineThread
{
    private const int MaxCommandsPerIteration = 64;
    private const int MaxConsecutiveFailures = 120;
    private const int IdleWaitMs = 50;

    private readonly SemaphoreSlim _decodeSlots;
    private readonly ConcurrentQueue<DecodeResult> _decoded = new();
    private readonly Queue<AssetLoadRequest> _waiting = new();
    private readonly ManualResetEventSlim _wake = new(false);
    private readonly ManualResetEventSlim _ready = new(false);
    private readonly CancellationTokenSource _decodeCancellation = new();
    private int _consecutiveFailures;

    internal AssetThread()
    {
        _decodeSlots = new SemaphoreSlim(Math.Max(1, Application.AssetSetting.DecodeConcurrency));
    }

    public override bool IsBackground => true;

    public override string ThreadName => "AssetThread";

    /// <summary>Waits until this worker has attached to the shared request queue.</summary>
    internal bool WaitUntilReady(TimeSpan timeout) => _ready.Wait(timeout);

    /// <summary>Wakes the worker so a submission that just arrived is picked up without waiting for the idle tick.</summary>
    internal void Signal() => _wake.Set();

    protected override void Initialize()
    {
        AssetSystem.AttachWorker(this);
        Log.AttachIoOwner();
        _ready.Set();
    }

    protected override void Run()
    {
        try
        {
            // File logging is IO like any other: this thread writes the queued records, bounded per loop.
            Log.PumpFileIo();
            DrainDecoded();
            DrainCommands();
            AssetSystem.Store.ProcessDue(Environment.TickCount64);
            Interlocked.Exchange(ref _consecutiveFailures, 0);
        }
        catch (OperationCanceledException) when (StopToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            // Loop-level degradation: a failing iteration is logged and the loop keeps going.
            Log.Exception(error);
            if (Interlocked.Increment(ref _consecutiveFailures) >= MaxConsecutiveFailures)
            {
                Log.Error($"Asset loop stopped after {MaxConsecutiveFailures} consecutive failed iterations.");
                RequestStop();
            }
        }

        WaitForWork();
    }

    // Commands keep their submission order; a load that finds no decode slot waits in a local queue instead of blocking.
    private void DrainCommands()
    {
        while (_waiting.Count > 0 && _decodeSlots.CurrentCount > 0)
        {
            StartDecode(_waiting.Dequeue());
        }

        int processed = 0;
        while (processed < MaxCommandsPerIteration && AssetSystem.TryDequeueCommand(out AssetCommand command))
        {
            processed++;

            switch (command.Kind)
            {
                case AssetCommandKind.Unload:
                    AssetSystem.Store.Unload(
                        command.AssetId,
                        Environment.TickCount64,
                        defer: true,
                        Application.AssetSetting.DeferredReleaseWindow);
                    break;

                case AssetCommandKind.Load when command.Request is { } request:
                    if (request.CancellationToken.IsCancellationRequested)
                    {
                        AssetSystem.CompleteCanceled(request);
                        break;
                    }

                    if (AssetSystem.Store.TryReuse(request.SourcePath, out IAsset? cached))
                    {
                        AssetSystem.CompleteLoaded(request, cached!);
                        break;
                    }

                    if (_decodeSlots.CurrentCount <= 0)
                    {
                        _waiting.Enqueue(request);
                        return;
                    }

                    StartDecode(request);
                    break;
            }
        }
    }

    private void StartDecode(AssetLoadRequest request)
    {
        _decodeSlots.Wait();
        AssetSystem.IncrementInFlight();

        IAssetLoader loader = request.Loader;
        CancellationToken requestCancellation = request.CancellationToken;
        CancellationToken engineCancellation = _decodeCancellation.Token;

        _ = Task.Run(() =>
        {
            AssetDraft? draft = null;
            Exception? error = null;
            var canceled = false;

            try
            {
                engineCancellation.ThrowIfCancellationRequested();
                requestCancellation.ThrowIfCancellationRequested();

                draft = loader.Load(request.SourcePath);

                engineCancellation.ThrowIfCancellationRequested();
                requestCancellation.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
                when (requestCancellation.IsCancellationRequested || engineCancellation.IsCancellationRequested)
            {
                canceled = true;
            }
            catch (Exception caught)
            {
                error = caught;
            }
            finally
            {
                _decodeSlots.Release();
                _decoded.Enqueue(new DecodeResult(request, draft, error, canceled));
                _wake.Set();
            }
        });
    }

    // Registration happens here, on the owner thread: the decode itself never touches shared state.
    private void DrainDecoded()
    {
        while (_decoded.TryDequeue(out DecodeResult result))
        {
            AssetSystem.DecrementInFlight();

            if (result.Canceled)
            {
                AssetSystem.CompleteCanceled(result.Request);
                continue;
            }

            if (result.Error is not null)
            {
                Log.Exception(result.Error);
                AssetSystem.CompleteFailed(result.Request, result.Error);
                continue;
            }

            try
            {
                IAsset asset = AssetSystem.Store.Register(result.Request.SourcePath, result.Draft!);
                AssetSystem.CompleteLoaded(result.Request, asset);
            }
            catch (Exception error)
            {
                Log.Exception(error);
                AssetSystem.CompleteFailed(result.Request, error);
            }
        }
    }

    private void WaitForWork()
    {
        _wake.Wait(IdleWaitMs);
        _wake.Reset();
    }

    protected override void Shutdown()
    {
        _decodeCancellation.Cancel();

        // Requests that never reached the pool are cancelled so no caller waits forever.
        while (_waiting.Count > 0)
            AssetSystem.CompleteCanceled(_waiting.Dequeue());

        while (AssetSystem.TryDequeueCommand(out AssetCommand command))
        {
            if (command.Request is { } request)
                AssetSystem.CompleteCanceled(request);
        }

        // Bounded drain: in-flight decodes publish their result (or their cancellation) before the store is detached.
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (AssetSystem.InFlight > 0 && stopwatch.Elapsed < Application.AssetSetting.ShutdownTimeout)
        {
            _wake.Wait(5);
            _wake.Reset();
            DrainDecoded();
        }

        DrainDecoded();
        AssetSystem.DetachWorker(this);
        _ready.Reset();

        // Final drain before the stop log, so queued records keep their order relative to it.
        Log.PumpFileIo(int.MaxValue);

        if (Application.LogSetting.EnableMainThreadLog)
        {
            Log.Debug("Asset thread stopped.");
        }

        // Ownership is released last: shutdown logs from other threads then go straight to the file.
        Log.DetachIoOwner();
    }
}
