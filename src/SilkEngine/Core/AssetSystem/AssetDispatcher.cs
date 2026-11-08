using System.Collections.Concurrent;
using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;
using SilkEngine.Runtime.Host;

namespace SilkEngine.Core.AssetSystem;

/// <summary>Transport between the asset API and the main-thread asset owner.</summary>
/// <remarks>Commands are queued in submission order and executed by <see cref="SilkEngine.Runtime.Host.MainLoop"/>
/// through an asset pump; async completions are queued back and must be pumped by the main loop. Blocking
/// requests complete on the owner thread directly, so a blocked caller never depends on the pump.</remarks>
internal sealed class AssetDispatcher
{
    private readonly ConcurrentQueue<AssetCommand> _commands = new();
    private readonly ConcurrentQueue<AssetCompletion> _completions = new();
    private readonly ConcurrentQueue<AssetId> _ready = new();
    private readonly ConcurrentQueue<AssetId> _released = new();
    private readonly ConcurrentQueue<AssetId> _pinReleased = new();

    private int _ownerThreadId;
    private int _inFlight;

    /// <summary>Ready ids, owned by the store and drained by the render thread.</summary>
    internal ConcurrentQueue<AssetId> Ready => _ready;

    /// <summary>Released ids, owned by the store and drained by the render thread.</summary>
    internal ConcurrentQueue<AssetId> Released => _released;

    /// <summary>Ids whose cache pin was released while render references remain; drained by the render thread.</summary>
    internal ConcurrentQueue<AssetId> PinReleased => _pinReleased;

    /// <summary>True once the main loop attached itself as the store owner.</summary>
    internal bool HasOwner => Volatile.Read(ref _ownerThreadId) != 0;

    /// <summary>True while the calling thread is the asset store owner (the engine main loop).</summary>
    internal bool IsOwnerThread
    {
        get
        {
            int ownerThreadId = Volatile.Read(ref _ownerThreadId);
            return ownerThreadId != 0 && Environment.CurrentManagedThreadId == ownerThreadId;
        }
    }

    /// <summary>Requests waiting in the queue (in-flight decodes are not included).</summary>
    internal int QueuedCount => _commands.Count;

    /// <summary>Queued requests plus requests currently decoding on the thread pool.</summary>
    internal int PendingCount => _commands.Count + Volatile.Read(ref _inFlight);

    /// <summary>Requests currently decoding on the thread pool.</summary>
    internal int InFlight => Volatile.Read(ref _inFlight);

    internal void Submit(AssetCommand command) => _commands.Enqueue(command);

    internal bool TryDequeueCommand(out AssetCommand command) => _commands.TryDequeue(out command);

    internal bool TryTakeReady(out AssetId assetId) => _ready.TryDequeue(out assetId);

    internal bool TryTakeReleased(out AssetId assetId) => _released.TryDequeue(out assetId);

    internal bool TryTakePinReleased(out AssetId assetId) => _pinReleased.TryDequeue(out assetId);

    internal void IncrementInFlight() => Interlocked.Increment(ref _inFlight);

    internal int DecrementInFlight() => Interlocked.Decrement(ref _inFlight);

    internal void CompleteLoaded(AssetLoadRequest request, IAsset asset)
    {
        if (request.Blocking)
        {
            request.Completion.TrySetResult(asset);
            return;
        }

        _completions.Enqueue(AssetCompletion.Loaded(request, asset));
    }

    internal void CompleteFailed(AssetLoadRequest request, Exception error)
    {
        if (request.Blocking)
        {
            request.Completion.TrySetException(error);
            return;
        }

        _completions.Enqueue(AssetCompletion.Failed(request, error));
    }

    internal void CompleteCanceled(AssetLoadRequest request)
    {
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

    internal void AttachOwner(int? threadId = null) =>
        Volatile.Write(ref _ownerThreadId, threadId ?? Environment.CurrentManagedThreadId);

    internal void DetachOwner() => Volatile.Write(ref _ownerThreadId, 0);
}

