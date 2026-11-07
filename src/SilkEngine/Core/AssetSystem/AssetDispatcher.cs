using System.Collections.Concurrent;
using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;
using SilkEngine.Runtime.Host;

namespace SilkEngine.Core.AssetSystem;

/// <summary>Transport between the business asset API and the asset worker thread.</summary>
/// <remarks>Requests are queued in submission order and executed by <see cref="AssetThread"/>; results are queued
/// back and must be pumped by the main loop, so user code always runs on the main thread. The ready/released queues
/// are handed to <see cref="AssetStore"/>, which announces a record only after publishing its snapshot.</remarks>
internal sealed class AssetDispatcher
{
    private readonly ConcurrentQueue<AssetCommand> _commands = new();
    private readonly ConcurrentQueue<AssetCompletion> _completions = new();
    private readonly ConcurrentQueue<AssetId> _ready = new();
    private readonly ConcurrentQueue<AssetId> _released = new();

    private AssetThread? _worker;
    private int _workerThreadId;
    private int _inFlight;

    /// <summary>Ready ids, owned by the store and drained by the render thread.</summary>
    internal ConcurrentQueue<AssetId> Ready => _ready;

    /// <summary>Released ids, owned by the store and drained by the render thread.</summary>
    internal ConcurrentQueue<AssetId> Released => _released;

    /// <summary>The started asset worker, if any.</summary>
    internal AssetThread? Worker => Volatile.Read(ref _worker);

    /// <summary>True while the calling thread is the asset worker thread.</summary>
    internal bool IsWorkerThread
    {
        get
        {
            int workerThreadId = Volatile.Read(ref _workerThreadId);
            return workerThreadId != 0 && Environment.CurrentManagedThreadId == workerThreadId;
        }
    }

    /// <summary>Requests waiting in the queue (in-flight decodes are not included).</summary>
    internal int QueuedCount => _commands.Count;

    /// <summary>Queued requests plus requests currently decoding on the thread pool.</summary>
    internal int PendingCount => _commands.Count + Volatile.Read(ref _inFlight);

    /// <summary>Requests currently decoding on the thread pool.</summary>
    internal int InFlight => Volatile.Read(ref _inFlight);

    internal void Submit(AssetCommand command)
    {
        _commands.Enqueue(command);
        Worker?.Signal();
    }

    internal bool TryDequeueCommand(out AssetCommand command) =>
        _commands.TryDequeue(out command);

    internal bool TryTakeReady(out AssetId assetId) => _ready.TryDequeue(out assetId);

    internal bool TryTakeReleased(out AssetId assetId) => _released.TryDequeue(out assetId);

    internal void IncrementInFlight() => Interlocked.Increment(ref _inFlight);

    internal int DecrementInFlight() => Interlocked.Decrement(ref _inFlight);

    internal void CompleteLoaded(AssetLoadRequest request, IAsset asset) =>
        _completions.Enqueue(AssetCompletion.Loaded(request, asset));

    internal void CompleteFailed(AssetLoadRequest request, Exception error) =>
        _completions.Enqueue(AssetCompletion.Failed(request, error));

    internal void CompleteCanceled(AssetLoadRequest request) =>
        _completions.Enqueue(AssetCompletion.Canceled(request));

    /// <summary>Pumps completed requests on the main thread, bounded by <paramref name="maxCount"/>.</summary>
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

    internal void AttachWorker(AssetThread worker)
    {
        Volatile.Write(ref _worker, worker);
        Volatile.Write(ref _workerThreadId, Environment.CurrentManagedThreadId);
    }

    internal void DetachWorker(AssetThread worker)
    {
        if (ReferenceEquals(Volatile.Read(ref _worker), worker))
            Volatile.Write(ref _worker, null);

        Volatile.Write(ref _workerThreadId, 0);
    }
}
