using System.Collections.Concurrent;
using System.IO;
using SilkEngine.Asset;
using SilkEngine.Runtime.Host;

namespace SilkEngine.Core.AssetSystem;

/// <summary>Static bridge between the business asset API and the asset worker thread.</summary>
/// <remarks>Requests are queued here in submission order and executed by <see cref="AssetThread"/>; results are
/// queued back and must be pumped by the main loop, so user code always runs on the main thread. Only the worker
/// thread (or <see cref="AssetInlineMode"/>) mutates <see cref="Store"/>; snapshots and the ready/release queues
/// stay readable from any thread.</remarks>
internal static class AssetSystem
{
    private static readonly ConcurrentQueue<AssetCommand> s_pending = new();
    private static readonly ConcurrentQueue<AssetCompletion> s_completions = new();
    private static readonly ConcurrentQueue<AssetId> s_ready = new();
    private static readonly ConcurrentQueue<AssetId> s_released = new();

    private static readonly AssetStore s_store = new(s_ready, s_released);

    // The registry is read once: runtime edits to Application.AssetLoaders are ignored.
    private static readonly Lazy<Dictionary<Type, IAssetLoader>> s_loaders = new(
        static () => new Dictionary<Type, IAssetLoader>(Application.AssetLoaders));

    private static AssetThread? s_worker;
    private static int s_workerThreadId;
    private static int s_inFlight;

    /// <summary>State owned by the worker thread.</summary>
    internal static AssetStore Store => s_store;

    /// <summary>The running worker, or <c>null</c> when no asset thread has been started.</summary>
    internal static AssetThread? Worker => Volatile.Read(ref s_worker);

    /// <summary>True while the calling thread is the asset worker thread.</summary>
    internal static bool IsWorkerThread
    {
        get
        {
            int workerThreadId = Volatile.Read(ref s_workerThreadId);
            return workerThreadId != 0 && Environment.CurrentManagedThreadId == workerThreadId;
        }
    }

    /// <summary>Queued requests plus requests currently decoding on the thread pool.</summary>
    internal static int PendingCount => s_pending.Count + Volatile.Read(ref s_inFlight);

    /// <summary>Assets inside their release window plus released ids the render thread has not taken yet.</summary>
    internal static int PendingReleaseCount => s_store.DeferredCount + s_released.Count;

    internal static bool TryTakeReady(out AssetId assetId) => s_ready.TryDequeue(out assetId);

    internal static bool TryTakeReleased(out AssetId assetId) => s_released.TryDequeue(out assetId);

    /// <summary>Loads synchronously; only valid on the asset thread or in inline mode.</summary>
    /// <exception cref="InvalidOperationException">Called from another thread while the worker owns the store.</exception>
    internal static T Load<T>(string sourcePath)
        where T : IAsset
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        IAssetLoader loader = ResolveLoader(typeof(T));
        string path = NormalizePath(sourcePath);

        if (!IsWorkerThread && !AssetInlineMode.IsActive)
        {
            throw new InvalidOperationException(
                "Assets.Load<T> is not available while the asset thread runs; use Assets.LoadAsync<T>.");
        }

        return Cast<T>(s_store.LoadNow(path, loader));
    }

    /// <summary>Queues an asynchronous load; the returned task completes on the main-thread pump.</summary>
    /// <exception cref="NotSupportedException">No loader is registered for <typeparamref name="T"/>.</exception>
    /// <exception cref="InvalidOperationException">The request queue is full.</exception>
    internal static Task<T> LoadAsync<T>(string sourcePath, CancellationToken cancellationToken)
        where T : IAsset
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        IAssetLoader loader = ResolveLoader(typeof(T));
        string path = NormalizePath(sourcePath);

        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<T>(cancellationToken);

        if (AssetInlineMode.IsActive)
            return LoadInline<T>(path, loader);

        int capacity = Application.AssetSetting.MaxPendingRequests;
        if (s_pending.Count >= capacity)
        {
            throw new InvalidOperationException(
                $"Asset request queue is full ({capacity} pending); the asset thread is not keeping up.");
        }

        var request = new AssetLoadRequest(path, loader, cancellationToken);
        Submit(AssetCommand.Load(request));

        return AwaitAs<T>(request.Completion.Task);
    }

    internal static bool TryGet<T>(AssetId assetId, out T? asset)
        where T : IAsset => s_store.TryGetById(assetId, out asset);

    /// <summary>Reads the path map; only valid on the asset thread or in inline mode.</summary>
    /// <exception cref="InvalidOperationException">Called from another thread while the worker owns the store.</exception>
    internal static bool TryGet<T>(string sourcePath, out T? asset)
        where T : IAsset
    {
        asset = default;
        if (string.IsNullOrWhiteSpace(sourcePath))
            return false;

        if (!IsWorkerThread && !AssetInlineMode.IsActive)
        {
            throw new InvalidOperationException(
                "Assets.TryGet<T>(string) reads the path map; call it on the asset thread or in inline mode.");
        }

        return s_store.TryGetByPath(NormalizePath(sourcePath), out asset);
    }

    /// <summary>Decrements the refcount; the asset thread or inline mode applies the release window.</summary>
    internal static bool Unload(AssetId assetId)
    {
        if (AssetInlineMode.IsActive || IsWorkerThread)
        {
            return s_store.Unload(
                assetId,
                Environment.TickCount64,
                defer: false,
                Application.AssetSetting.DeferredReleaseWindow);
        }

        if (Worker is null)
            return false;

        Submit(AssetCommand.Unload(assetId));
        return true;
    }

    /// <summary>Pumps completed requests on the main thread, bounded by <paramref name="maxCount"/>.</summary>
    internal static int DrainCompletions(int maxCount)
    {
        int drained = 0;
        while (drained < maxCount && s_completions.TryDequeue(out AssetCompletion completion))
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
                        new InvalidOperationException("Asset completion carried no result."));
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

    internal static void AttachWorker(AssetThread worker)
    {
        Volatile.Write(ref s_worker, worker);
        Volatile.Write(ref s_workerThreadId, Environment.CurrentManagedThreadId);
    }

    internal static void DetachWorker(AssetThread worker)
    {
        if (ReferenceEquals(Volatile.Read(ref s_worker), worker))
            Volatile.Write(ref s_worker, null);

        Volatile.Write(ref s_workerThreadId, 0);
    }

    internal static void SignalWorker() => Worker?.Signal();

    internal static bool TryDequeueCommand(out AssetCommand command) =>
        s_pending.TryDequeue(out command);

    internal static int InFlight => Volatile.Read(ref s_inFlight);

    internal static void IncrementInFlight() => Interlocked.Increment(ref s_inFlight);

    internal static int DecrementInFlight() => Interlocked.Decrement(ref s_inFlight);

    internal static void CompleteLoaded(AssetLoadRequest request, IAsset asset) =>
        s_completions.Enqueue(AssetCompletion.Loaded(request, asset));

    internal static void CompleteFailed(AssetLoadRequest request, Exception error) =>
        s_completions.Enqueue(AssetCompletion.Failed(request, error));

    internal static void CompleteCanceled(AssetLoadRequest request) =>
        s_completions.Enqueue(AssetCompletion.Canceled(request));

    private static void Submit(AssetCommand command)
    {
        s_pending.Enqueue(command);
        Worker?.Signal();
    }

    private static Task<T> LoadInline<T>(string path, IAssetLoader loader)
        where T : IAsset
    {
        try
        {
            return Task.FromResult(Cast<T>(s_store.LoadNow(path, loader)));
        }
        catch (Exception error)
        {
            return Task.FromException<T>(error);
        }
    }

    private static async Task<T> AwaitAs<T>(Task<IAsset> task)
        where T : IAsset
    {
        IAsset asset = await task.ConfigureAwait(false);
        return Cast<T>(asset);
    }

    private static T Cast<T>(IAsset asset)
        where T : IAsset
    {
        if (asset is T typed)
            return typed;

        throw new InvalidOperationException(
            $"Asset loader for '{typeof(T).FullName}' returned '{asset.GetType().FullName}'.");
    }

    private static IAssetLoader ResolveLoader(Type assetType)
    {
        if (s_loaders.Value.TryGetValue(assetType, out IAssetLoader? loader))
            return loader;

        throw new NotSupportedException(
            $"No asset loader is registered for '{assetType.FullName}'. Add it to Application.AssetLoaders before startup.");
    }

    private static string NormalizePath(string sourcePath) => Path.GetFullPath(sourcePath);
}
