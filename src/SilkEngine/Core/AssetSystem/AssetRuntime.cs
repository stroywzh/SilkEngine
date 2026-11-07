using System.Diagnostics.CodeAnalysis;
using System.IO;
using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Core.AssetSystem;

/// <summary>Internal composition root for the asset pipeline; the business facade forwards here.</summary>
/// <remarks>Owns the dispatcher (queues + worker identity), the single-owner store, catalog/loader resolution and
/// the startup bootstrap host. Business code uses <see cref="SilkEngine.Assets"/>; only engine internals compose
/// this type.</remarks>
internal static class AssetRuntime
{
    internal static AssetDispatcher Dispatcher { get; } = new();

    internal static AssetResolver Resolver { get; } = new();

    internal static AssetBootstrapHost Bootstrap { get; } = new();

    internal static AssetStore Store { get; } = new(Dispatcher.Ready, Dispatcher.Released);

    /// <summary>Attaches the read-only catalog used to resolve source paths and persistent identities.</summary>
    internal static void AttachCatalog(IAssetCatalog catalog) => Resolver.AttachCatalog(catalog);

    /// <summary>Loads synchronously by source path; only valid on the asset thread.</summary>
    internal static T Load<T>(string sourcePath)
        where T : IAsset
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        // The single-writer guard must hold in every build and fires before catalog or loader resolution.
        EnsureWorkerThread();

        IAssetLoader loader = Resolver.ResolveLoader(typeof(T));
        AssetSource source = Resolver.ResolveSource(sourcePath);
        return Cast<T>(Store.LoadNow(source, loader));
    }

    /// <summary>Loads synchronously by persistent identity; only valid on the asset thread.</summary>
    internal static T Load<T>(AssetRef reference)
        where T : IAsset
    {
        // The single-writer guard fires before catalog or loader resolution.
        EnsureWorkerThread();

        IAssetLoader loader = Resolver.ResolveLoader(typeof(T));
        AssetSource source = Resolver.ResolveSource(reference);
        return Cast<T>(Store.LoadNow(source, loader));
    }

    /// <summary>Queues an asynchronous load by source path; the task completes on the main-thread pump.</summary>
    internal static Task<T> LoadAsync<T>(string sourcePath, CancellationToken cancellationToken)
        where T : IAsset
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        IAssetLoader loader = Resolver.ResolveLoader(typeof(T));
        AssetSource source = Resolver.ResolveSource(sourcePath);
        return LoadAsyncCore<T>(source, loader, cancellationToken);
    }

    /// <summary>Queues an asynchronous load by persistent identity; the task completes on the main-thread pump.</summary>
    internal static Task<T> LoadAsync<T>(AssetRef reference, CancellationToken cancellationToken)
        where T : IAsset
    {
        IAssetLoader loader = Resolver.ResolveLoader(typeof(T));
        AssetSource source = Resolver.ResolveSource(reference);
        return LoadAsyncCore<T>(source, loader, cancellationToken);
    }

    internal static bool TryGet<T>(AssetId assetId, out T? asset)
        where T : IAsset
    {
        if (Store.TryGet(assetId, out AssetView view) && view.Asset is T typed)
        {
            asset = typed;
            return true;
        }

        asset = default;
        return false;
    }

    internal static bool TryGetView(AssetId assetId, out AssetView view) =>
        Store.TryGet(assetId, out view);

    /// <summary>Resolves the loaded asset for a runtime binding without exposing the runtime view.</summary>
    internal static bool TryGetRenderAsset(
        AssetId assetId,
        [MaybeNullWhen(false)] out IAsset asset
    )
    {
        if (Store.TryGet(assetId, out AssetView view))
        {
            asset = view.Asset;
            return true;
        }

        asset = null!;
        return false;
    }

    /// <summary>Takes the decoded payload of a loaded asset (one-shot, render bridge).</summary>
    internal static bool TryTakePayload<T>(AssetId assetId, [MaybeNullWhen(false)] out T payload)
        where T : class, IAssetPayload
    {
        payload = null!;
        return Store.TryGet(assetId, out AssetView view) && view.Payload.TryTake<T>(out payload);
    }

    internal static bool TryGet<T>(string sourcePath, out T? asset)
        where T : IAsset
    {
        asset = default;
        if (string.IsNullOrWhiteSpace(sourcePath))
            return false;

        if (!Dispatcher.IsWorkerThread)
        {
            throw new InvalidOperationException(
                "Assets.TryGet<T>(string) reads the path map; call it on the asset thread."
            );
        }

        return Store.TryGetByPath(NormalizePath(sourcePath), out asset);
    }

    /// <summary>Decrements the refcount; the asset thread applies the release window.</summary>
    internal static bool Release(AssetId assetId)
    {
        if (Dispatcher.IsWorkerThread)
        {
            bool unloaded = Store.Unload(
                assetId,
                Environment.TickCount64,
                defer: false,
                Application.AssetSetting.DeferredReleaseWindow
            );

            // The synchronous path must not leave the release invisible to readers.
            Store.Commit();
            return unloaded;
        }

        if (Dispatcher.Worker is null)
            return false;

        Dispatcher.Submit(AssetCommand.Unload(assetId));
        return true;
    }

    internal static bool TryTakeReady(out AssetId assetId) => Dispatcher.TryTakeReady(out assetId);

    internal static bool TryTakeReleased(out AssetId assetId) =>
        Dispatcher.TryTakeReleased(out assetId);

    /// <summary>Queued requests plus requests currently decoding on the thread pool.</summary>
    internal static int PendingCount => Dispatcher.PendingCount;

    /// <summary>Assets inside their release window plus released ids the render thread has not taken yet.</summary>
    internal static int PendingReleaseCount => Store.DeferredCount + Dispatcher.Released.Count;

    // Both overloads share this: resolve first, then check cancellation, capacity and submit.
    private static Task<T> LoadAsyncCore<T>(
        AssetSource source,
        IAssetLoader loader,
        CancellationToken cancellationToken
    )
        where T : IAsset
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<T>(cancellationToken);

        int capacity = Application.AssetSetting.MaxPendingRequests;
        if (Dispatcher.QueuedCount >= capacity)
        {
            throw new InvalidOperationException(
                $"Asset request queue is full ({capacity} pending); the asset thread is not keeping up."
            );
        }

        var request = new AssetLoadRequest(source, loader, cancellationToken);
        Dispatcher.Submit(AssetCommand.Load(request));

        return AwaitAs<T>(request.Completion.Task);
    }

    private static void EnsureWorkerThread()
    {
        if (Dispatcher.IsWorkerThread)
            return;

        throw new InvalidOperationException(
            "Assets.Load<T> is not available while the asset thread runs; use Assets.LoadAsync<T>."
        );
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
            $"Asset loader for '{typeof(T).FullName}' returned '{asset.GetType().FullName}'."
        );
    }

    private static string NormalizePath(string sourcePath) => Path.GetFullPath(sourcePath);
}
