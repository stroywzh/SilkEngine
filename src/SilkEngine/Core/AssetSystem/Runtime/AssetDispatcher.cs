using System.Collections.Concurrent;
using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;

namespace SilkEngine.AssetSystem.Runtime;

/// <summary>Transport between the asset API and the main-thread asset owner.</summary>
/// <remarks>Commands are queued in submission order and executed by <see cref="SilkEngine.Runtime.Host.MainLoop"/>
/// through an asset pump; async completions are queued back and must be pumped by the main loop. Blocking
/// requests complete on the owner thread directly, so a blocked caller never depends on the pump.</remarks>
internal sealed class AssetDispatcher
{
    private readonly ConcurrentQueue<AssetCommand> _commands = new();
    private readonly ConcurrentQueue<AssetCompletion> _completions = new();
    private static readonly ConcurrentQueue<AssetId> _ready = new();
    private static readonly ConcurrentQueue<AssetId> _released = new();
    private static readonly ConcurrentQueue<AssetId> _pinReleased = new();

    // Set by the engine main loop from its own thread after the sync context is installed; 0 until attached.
    private int _ownerThreadId;

    // Admitted loads that have not completed yet: queued, waiting for a decode slot, or decoding.
    private int _pendingLoads;

    /// <summary>True while an engine main loop has attached itself as the asset owner.</summary>
    internal bool HasOwner => Volatile.Read(ref _ownerThreadId) != 0;

    /// <summary>Binds the owner thread; the engine main loop calls this on its own thread.</summary>
    internal void AttachOwner(int threadId)
    {
        if (threadId == 0)
            throw new ArgumentOutOfRangeException(nameof(threadId));

        Volatile.Write(ref _ownerThreadId, threadId);
    }

    /// <summary>Unbinds the owner thread; the asset pump calls this during shutdown.</summary>
    internal void DetachOwner() => Volatile.Write(ref _ownerThreadId, 0);

    /// <summary>Ready ids, owned by the store and drained by the render thread.</summary>
    internal ConcurrentQueue<AssetId> Ready => _ready;

    /// <summary>Released ids, owned by the store and drained by the render thread.</summary>
    internal ConcurrentQueue<AssetId> Released => _released;

    /// <summary>Ids whose cache pin was released while render references remain; drained by the render thread.</summary>
    internal ConcurrentQueue<AssetId> PinReleased => _pinReleased;

    /// <summary>True while the calling thread is the asset store owner (the engine main loop).</summary>
    internal bool IsOwnerThread
    {
        get
        {
            int ownerThreadId = Volatile.Read(ref _ownerThreadId);
            return ownerThreadId != 0 && Environment.CurrentManagedThreadId == ownerThreadId;
        }
    }

    /// <summary>Commands waiting in the queue; non-load commands are included.</summary>
    internal int QueuedCount => _commands.Count;

    /// <summary>Admitted load requests that have not completed yet, including in-flight decodes.</summary>
    internal int PendingCount => Volatile.Read(ref _pendingLoads);

    internal void Submit(AssetCommand command) => _commands.Enqueue(command);

    /// <summary>Admits a load request within <paramref name="maxPending"/>; false when the budget is exhausted.</summary>
    internal bool TrySubmitLoad(AssetCommand command, int maxPending)
    {
        AssetLoadRequest request =
            command.Request
            ?? throw new InvalidOperationException("A load command must carry a request.");

        while (true)
        {
            int pending = Volatile.Read(ref _pendingLoads);
            if (maxPending > 0 && pending >= maxPending)
                return false;

            if (Interlocked.CompareExchange(ref _pendingLoads, pending + 1, pending) == pending)
                break;
        }

        request.Admit();
        _commands.Enqueue(command);
        return true;
    }

    /// <summary>Releases one admitted load; safe to call more than once for the same request.</summary>
    internal void ReleaseAdmission(AssetLoadRequest request)
    {
        if (request.ReleaseAdmission())
            Interlocked.Decrement(ref _pendingLoads);
    }

    internal bool TryDequeueCommand(out AssetCommand command) => _commands.TryDequeue(out command);

    internal bool TryTakeReady(out AssetId assetId) => _ready.TryDequeue(out assetId);

    internal bool TryTakeReleased(out AssetId assetId) => _released.TryDequeue(out assetId);

    internal bool TryTakePinReleased(out AssetId assetId) => _pinReleased.TryDequeue(out assetId);

    internal void CompleteLoaded(AssetLoadRequest request, IAsset asset)
    {
        ReleaseAdmission(request);
        if (request.Blocking)
        {
            request.Completion.TrySetResult(asset);
            return;
        }

        _completions.Enqueue(AssetCompletion.Loaded(request, asset));
    }

    internal void CompleteFailed(AssetLoadRequest request, Exception error)
    {
        ReleaseAdmission(request);
        if (request.Blocking)
        {
            request.Completion.TrySetException(error);
            return;
        }

        _completions.Enqueue(AssetCompletion.Failed(request, error));
    }

    internal void CompleteCanceled(AssetLoadRequest request)
    {
        ReleaseAdmission(request);
        if (request.Blocking)
        {
            request.Completion.TrySetCanceled(request.CancellationToken);
            return;
        }

        _completions.Enqueue(AssetCompletion.Canceled(request));
    }

    /// <summary>Pumps completed async requests on the main thread, bounded by <paramref name="maxCount"/>.</summary>
    internal int DrainCompletions(int maxCount)
    {
        int drained = 0;
        while (drained < maxCount && _completions.TryDequeue(out AssetCompletion completion))
        {
            drained++;
            try
            {
                TaskCompletionSource<IAsset> completionSource = completion.Request.Completion;
                if (completion.IsCanceled)
                {
                    completionSource.TrySetCanceled(completion.Request.CancellationToken);
                }
                else if (completion.Failure is not null)
                {
                    completionSource.TrySetException(completion.Failure.SourceException);
                }
                else if (completion.Asset is not null)
                {
                    completionSource.TrySetResult(completion.Asset);
                }
                else
                {
                    completionSource.TrySetException(
                        new InvalidOperationException("Asset completion carried no result.")
                    );
                }
            }
            catch (Exception error)
            {
                // A failing completion must not stop the pump or the frame.
                Log.Exception(error);
            }
        }

        return drained;
    }
}
