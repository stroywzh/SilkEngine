using System.Collections.Concurrent;
using System.Diagnostics;
using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;
using SilkEngine.Core.AssetSystem;

namespace SilkEngine.Runtime.Host;

/// <summary>Owner-thread asset work driven once per main-loop frame.</summary>
/// <remarks>The engine main loop owns the store: this pump drains commands and decoded drafts, registers,
/// advances the store clock, publishes one snapshot per frame and scans reference counts for deferred releases.
/// Decoding itself stays on the thread pool. Blocking load waiters are completed on the owner thread directly and
/// never depend on the async completion queue.</remarks>
internal sealed class AssetPump
{
    private const int MaxCommandsPerPump = 64;
    private const int MaxDecodedPerPump = 128;

    private readonly SemaphoreSlim _decodeSlots;
    private readonly ConcurrentQueue<DecodeResult> _decoded = new();
    private readonly Queue<AssetLoadRequest> _waiting = new();
    private readonly Dictionary<string, List<AssetLoadRequest>> _inFlight = new(
        StringComparer.Ordinal
    );
    private readonly HashSet<AssetLoadRequest> _inFlightRequests = new();
    private readonly CancellationTokenSource _decodeCancellation = new();
    private readonly object _decodeGate = new();

    private int _inFlightCount;
    private int _stopped;
    private int _pumping;

    // The store clock advances once per frame; a blocking owner-thread load may pump several times in one frame.
    private ulong _lastAdvanceFrame;

    internal AssetPump()
    {
        _decodeSlots = new SemaphoreSlim(
            Math.Max(1, Application.AssetSetting.DecodeConcurrency)
        );
    }

    internal bool IsPumping => Volatile.Read(ref _pumping) != 0;

    internal bool IsStopped => Volatile.Read(ref _stopped) != 0;

    /// <summary>Attaches the owner identity, runs the content bootstrap and freezes the loader table.</summary>
    internal void Start()
    {
        AssetRuntime.Dispatcher.AttachOwner(MainThreadSynchronizationContext.MainThreadId);
        AssetRuntime.AttachPump(this);
        try
        {
            // Freeze the loader table before the bootstrap signals completion so a load racing with
            // EngineHost.Start cannot observe an uninitialized pipeline. Content loaders are engine
            // defaults registered by MainLoop; the bootstrap only imports/attaches content.
            AssetRuntime.InitializeLoaders();
            AssetRuntime.StartAcceptingLoads();
            AssetRuntime.Bootstrap.Run();
        }
        catch
        {
            AssetRuntime.DetachPump(this);
            AssetRuntime.Dispatcher.DetachOwner();
            throw;
        }
    }

    /// <summary>Runs one frame of owner-thread asset work.</summary>
    /// <remarks>Re-entrant calls return immediately; a blocking owner-thread load drives this method directly
    /// and must not nest a second pump inside it.</remarks>
    internal void Pump()
    {
        if (Interlocked.Exchange(ref _pumping, 1) != 0)
            return;

        try
        {
            if (Volatile.Read(ref _stopped) != 0)
                return;

            Log.PumpFileIo();
            DrainDecoded();
            DrainCommands();

            ulong frameIndex = Time.FrameIndex;
            if (frameIndex != _lastAdvanceFrame)
            {
                AssetRuntime.Store.Advance(Time.DeltaSeconds);
                _lastAdvanceFrame = frameIndex;
            }

            AssetRuntime.Store.Commit();
        }
        finally
        {
            Volatile.Write(ref _pumping, 0);
        }
    }

    /// <summary>Stop accepting work, fail in-flight requests after the shutdown timeout and detach the owner.</summary>
    internal void Shutdown()
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
            return;

        AssetRuntime.StopAcceptingLoads();
        _decodeCancellation.Cancel();

        CancelQueuedAndWaiting();

        var stopwatch = Stopwatch.StartNew();
        while (
            Volatile.Read(ref _inFlightCount) > 0
            && stopwatch.Elapsed < Application.AssetSetting.ShutdownTimeout
        )
        {
            DrainDecoded();
            Thread.Sleep(5);
        }

        // Late decodes see Abandoned and dispose their draft instead of enqueueing it; callers already failed.
        lock (_decodeGate)
        {
            foreach (AssetLoadRequest request in _inFlightRequests.ToArray())
            {
                request.Abandoned = true;
                request.Completion.TrySetException(
                    new OperationCanceledException(
                        "The asset pipeline stopped before the request completed."
                    )
                );
            }
        }

        DrainDecoded();

        // Release every retention (assets hold their own handle), then publish removals and release payloads
        // before the owner detaches.
        AssetRuntime.ReleaseAllRetention();
        AssetRuntime.Store.Commit();

        AssetRuntime.DetachPump(this);
        AssetRuntime.Dispatcher.DetachOwner();
    }

    private void CancelQueuedAndWaiting()
    {
        while (_waiting.Count > 0)
            AssetRuntime.Dispatcher.CompleteCanceled(_waiting.Dequeue());

        while (AssetRuntime.Dispatcher.TryDequeueCommand(out AssetCommand command))
        {
            switch (command.Kind)
            {
                case AssetCommandKind.Load when command.Request is { } request:
                    AssetRuntime.Dispatcher.CompleteCanceled(request);
                    break;

                case AssetCommandKind.Unload:
                    AssetRuntime.Store.Unload(
                        command.AssetId,
                        Environment.TickCount64,
                        defer: false,
                        Application.AssetSetting.DeferredReleaseWindow
                    );
                    break;

                case AssetCommandKind.ReleaseRenderRef:
                    AssetRuntime.Store.ReleaseRenderRef(command.AssetId);
                    break;
            }
        }
    }

    private void DrainCommands()
    {
        // Requests that found no decode slot are retried before new commands; a cache hit or another in-flight
        // decode for the same path still completes them without touching the pool.
        while (_waiting.Count > 0 && _decodeSlots.CurrentCount > 0)
            TryDispatch(_waiting.Dequeue());

        int processed = 0;
        while (
            processed < MaxCommandsPerPump
            && AssetRuntime.Dispatcher.TryDequeueCommand(out AssetCommand command)
        )
        {
            processed++;

            switch (command.Kind)
            {
                case AssetCommandKind.Unload:
                    AssetRuntime.Store.Unload(
                        command.AssetId,
                        Environment.TickCount64,
                        defer: true,
                        Application.AssetSetting.DeferredReleaseWindow
                    );
                    break;

                case AssetCommandKind.ReleaseRenderRef:
                    AssetRuntime.Store.ReleaseRenderRef(command.AssetId);
                    break;

                case AssetCommandKind.Load when command.Request is { } request:
                    TryDispatch(request);
                    break;
            }
        }
    }

    private bool TryDispatch(AssetLoadRequest request)
    {
        if (request.Abandoned || request.CancellationToken.IsCancellationRequested)
        {
            AssetRuntime.Dispatcher.CompleteCanceled(request);
            return false;
        }

        string path = request.SourcePath;

        if (_inFlight.TryGetValue(path, out List<AssetLoadRequest>? waiters))
        {
            waiters.Add(request);
            _inFlightRequests.Add(request);
            return false;
        }

        if (AssetRuntime.Store.TryReuse(path, out IAsset? cached))
        {
            AssetRuntime.Dispatcher.CompleteLoaded(request, cached!);
            return false;
        }

        if (_decodeSlots.CurrentCount <= 0)
        {
            _waiting.Enqueue(request);
            return false;
        }

        StartDecode(request, path);
        return true;
    }

    private void StartDecode(AssetLoadRequest request, string path)
    {
        _decodeSlots.Wait();
        Interlocked.Increment(ref _inFlightCount);
        _inFlight[path] = new List<AssetLoadRequest> { request };
        _inFlightRequests.Add(request);

        IAssetLoader loader = request.Loader;
        AssetSource source = request.Source;
        CancellationToken engineCancellation = _decodeCancellation.Token;

        _ = Task.Run(() =>
        {
            AssetDraft? draft = null;
            Exception? error = null;
            var canceled = false;

            try
            {
                engineCancellation.ThrowIfCancellationRequested();

                draft = loader.Load(source);

                // A caller's cancellation token is applied when the group completes: the shared decode may
                // have other waiters that did not cancel, so it must not be aborted on behalf of one caller.
                engineCancellation.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (engineCancellation.IsCancellationRequested)
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

                bool abandoned;
                lock (_decodeGate)
                {
                    abandoned = request.Abandoned;
                    if (abandoned)
                    {
                        draft?.Dispose();
                        draft = null;
                    }
                    else
                    {
                        _decoded.Enqueue(new DecodeResult(request, draft, error, canceled));
                    }
                }

                if (abandoned)
                    Interlocked.Decrement(ref _inFlightCount);
            }
        });
    }

    private void DrainDecoded()
    {
        int processed = 0;
        var completed = new List<(string Path, IAsset Asset, AssetLoadRequest Request)>();

        while (processed < MaxDecodedPerPump && _decoded.TryDequeue(out DecodeResult result))
        {
            processed++;
            Interlocked.Decrement(ref _inFlightCount);
            _inFlightRequests.Remove(result.Request);

            string path = result.Request.SourcePath;

            if (result.Request.Abandoned)
            {
                result.Draft?.Dispose();
                CompleteGroupCanceled(path, result.Request);
                continue;
            }

            if (result.Canceled)
            {
                result.Draft?.Dispose();
                CompleteGroupCanceled(path, result.Request);
                continue;
            }

            if (result.Error is not null)
            {
                result.Draft?.Dispose();
                CompleteGroupFailed(path, result.Request, result.Error);
                continue;
            }

            AssetDraft? draft = result.Draft;
            try
            {
                IAsset asset = AssetRuntime.Store.Register(path, draft!, out _);
                draft = null;
                completed.Add((path, asset, result.Request));
            }
            catch (Exception error)
            {
                draft?.Dispose();
                CompleteGroupFailed(path, result.Request, error);
            }
        }

        // One snapshot per pump: ready ids are flushed after every registration in this batch.
        if (completed.Count > 0)
            AssetRuntime.Store.Commit();

        foreach ((string path, IAsset asset, AssetLoadRequest request) in completed)
            CompleteGroupLoaded(path, request, asset);
    }

    private void CompleteGroupLoaded(string path, AssetLoadRequest origin, IAsset asset)
    {
        foreach (AssetLoadRequest request in TakeGroup(path, origin))
        {
            if (request.Abandoned || request.CancellationToken.IsCancellationRequested)
                AssetRuntime.Dispatcher.CompleteCanceled(request);
            else
                AssetRuntime.Dispatcher.CompleteLoaded(request, asset);
        }
    }

    private void CompleteGroupFailed(string path, AssetLoadRequest origin, Exception error)
    {
        foreach (AssetLoadRequest request in TakeGroup(path, origin))
        {
            if (request.Abandoned || request.CancellationToken.IsCancellationRequested)
                AssetRuntime.Dispatcher.CompleteCanceled(request);
            else
                AssetRuntime.Dispatcher.CompleteFailed(request, error);
        }
    }

    private void CompleteGroupCanceled(string path, AssetLoadRequest origin)
    {
        foreach (AssetLoadRequest request in TakeGroup(path, origin))
            AssetRuntime.Dispatcher.CompleteCanceled(request);
    }

    private List<AssetLoadRequest> TakeGroup(string path, AssetLoadRequest origin)
    {
        if (!_inFlight.Remove(path, out List<AssetLoadRequest>? waiters))
            return new List<AssetLoadRequest> { origin };

        foreach (AssetLoadRequest waiter in waiters)
            _inFlightRequests.Remove(waiter);

        return waiters;
    }
}