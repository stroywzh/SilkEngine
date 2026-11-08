using System.Diagnostics.CodeAnalysis;
using System.IO;
using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;
using SilkEngine.Runtime;
using SilkEngine.Runtime.Host;

namespace SilkEngine.Core.AssetSystem;

/// <summary>Internal composition root for the asset pipeline; the public facade forwards here.</summary>
/// <remarks>The engine main loop is the single owner of <see cref="AssetStore"/>: it drains commands, registers
/// decoded drafts, publishes snapshots and processes releases. Decoding itself runs on the thread pool.</remarks>
internal static class AssetRuntime
{
    private static int _loadsAccepted;
    private static AssetPump? _pump;

    internal static AssetDispatcher Dispatcher { get; } = new();

    internal static AssetResolver Resolver { get; } = new();

    internal static AssetBootstrapHost Bootstrap { get; } = new();

    internal static AssetStore Store { get; } = new(
        Dispatcher.Ready,
        Dispatcher.Released,
        Dispatcher.PinReleased
    );

    /// <summary>The main-loop pump currently driving owner-thread work, if the engine is running.</summary>
    internal static AssetPump? Pump => Volatile.Read(ref _pump);

    internal static void AttachPump(AssetPump pump) => Volatile.Write(ref _pump, pump);

    internal static void DetachPump(AssetPump pump)
    {
        if (ReferenceEquals(Volatile.Read(ref _pump), pump))
            Volatile.Write(ref _pump, null);
    }

    /// <summary>Attaches the read-only catalog used to resolve source paths and persistent identities.</summary>
    internal static void AttachCatalog(IAssetCatalog catalog) => Resolver.AttachCatalog(catalog);

    /// <summary>Freezes the loader table after the startup bootstrap registered every loader.</summary>
    internal static void InitializeLoaders() => Resolver.InitializeLoaders();

    internal static void StartAcceptingLoads() => Volatile.Write(ref _loadsAccepted, 1);

    internal static void StopAcceptingLoads() => Volatile.Write(ref _loadsAccepted, 0);

    /// <summary>Loads synchronously; owner thread decodes inline, other threads submit and block.</summary>
    internal static T Load<T>(string sourcePath)
        where T : IAsset
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        EnsureReady();

        if (ThreadRegistry.IsRenderThread)
        {
            throw new InvalidOperationException(
                "Assets.Load<T> must not run on the render thread; use LoadAsync<T> or the render bridge."
            );
        }

        IAssetLoader loader = Resolver.ResolveLoader(typeof(T));
        AssetSource source = Resolver.ResolveSource(sourcePath);

        if (Dispatcher.IsOwnerThread)
        {
            // Route an owner-thread blocking load through the same pump so it joins an in-flight decode for
            // the same path instead of decoding the payload a second time. The owner must drive the pump
            // itself while it blocks; the main loop is not available for that.
            AssetPump? pump = Pump;
            if (pump is null || pump.IsPumping || pump.IsStopped)
                return Cast<T>(Store.LoadNow(source, loader));

            var request = new AssetLoadRequest(
                source,
                loader,
                CancellationToken.None,
                blocking: true
            );
            Dispatcher.Submit(AssetCommand.Load(request));

            while (!request.Completion.Task.IsCompleted)
            {
                if (!Dispatcher.HasOwner || pump.IsStopped)
                    break;

                pump.Pump();
                if (!request.Completion.Task.IsCompleted)
                    Thread.Sleep(1);
            }

            return Cast<T>(request.Completion.Task.GetAwaiter().GetResult());
        }

        var blockingRequest = new AssetLoadRequest(
            source,
            loader,
            CancellationToken.None,
            blocking: true
        );
        Dispatcher.Submit(AssetCommand.Load(blockingRequest));
        IAsset asset = blockingRequest.Completion.Task.GetAwaiter().GetResult();
        return Cast<T>(asset);
    }

    /// <summary>Queues an asynchronous load; the task completes on the main-thread pump.</summary>
    internal static Task<T> LoadAsync<T>(string sourcePath, CancellationToken cancellationToken = default)
        where T : IAsset
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        EnsureReady();

        IAssetLoader loader = Resolver.ResolveLoader(typeof(T));
        AssetSource source = Resolver.ResolveSource(sourcePath);
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

    /// <summary>Borrows the published record; does not acquire a reference.</summary>
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

    /// <summary>Acquires one render-side reference and returns the asset.</summary>
    internal static bool TryAcquire<T>(AssetId assetId, [MaybeNullWhen(false)] out T asset)
        where T : IAsset
    {
        asset = default;
        if (!Store.TryGet(assetId, out AssetView view) || !view.Lifetime.TryAcquire())
            return false;

        if (view.Asset is T typed)
        {
            asset = typed;
            return true;
        }

        view.Lifetime.ReleaseRenderRef();
        return false;
    }

    /// <summary>Takes the decoded payload of a loaded asset (one-shot, render bridge).</summary>
    internal static bool TryTakePayload<T>(AssetId assetId, [MaybeNullWhen(false)] out T payload)
        where T : class, IAssetPayload
    {
        payload = null!;
        return Store.TryGet(assetId, out AssetView view) && view.Payload.TryTake<T>(out payload);
    }

    /// <summary>Reads the payload without taking ownership.</summary>
    internal static bool TryPeekPayload<T>(AssetId assetId, [MaybeNullWhen(false)] out T payload)
        where T : class, IAssetPayload
    {
        payload = null!;
        return Store.TryGet(assetId, out AssetView view) && view.Payload.TryPeek<T>(out payload);
    }

    /// <summary>Releases one render-side reference; a record leaves the snapshot only after its pin is gone.</summary>
    internal static bool Release(AssetId assetId)
    {
        if (Dispatcher.IsOwnerThread)
        {
            bool released = Store.ReleaseRenderRef(assetId);

            // The synchronous path must not leave the removal invisible to readers.
            Store.Commit();
            return released;
        }

        if (!Dispatcher.HasOwner)
        {
            Log.Warning(
                $"Asset {assetId.Value} release ignored: the asset pipeline is not running."
            );
            return false;
        }

        Dispatcher.Submit(AssetCommand.ReleaseRenderRef(assetId));
        return true;
    }

    /// <summary>Releases the cache pin for one asset; the scene lifecycle (not implemented yet) calls this.</summary>
    internal static bool Unload(AssetId assetId)
    {
        if (!assetId.IsValid)
            return false;

        if (Dispatcher.IsOwnerThread)
        {
            bool unloaded = Store.Unload(
                assetId,
                Environment.TickCount64,
                defer: true,
                Application.AssetSetting.DeferredReleaseWindow
            );

            Store.Commit();
            return unloaded;
        }

        if (!Dispatcher.HasOwner)
        {
            Log.Warning(
                $"Asset {assetId.Value} unload ignored: the asset pipeline is not running."
            );
            return false;
        }

        Dispatcher.Submit(AssetCommand.Unload(assetId));
        return true;
    }

    internal static bool TryTakeReady(out AssetId assetId) => Dispatcher.TryTakeReady(out assetId);

    internal static bool TryTakeReleased(out AssetId assetId) =>
        Dispatcher.TryTakeReleased(out assetId);

    internal static bool TryTakePinReleased(out AssetId assetId) =>
        Dispatcher.TryTakePinReleased(out assetId);

    /// <summary>Queued requests plus requests currently decoding on the thread pool.</summary>
    internal static int PendingCount => Dispatcher.PendingCount;

    /// <summary>Assets inside their release window plus released ids the render thread has not taken yet.</summary>
    internal static int PendingReleaseCount => Store.DeferredCount + Dispatcher.Released.Count;

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

        var request = new AssetLoadRequest(source, loader, cancellationToken, blocking: false);
        Dispatcher.Submit(AssetCommand.Load(request));

        return AwaitAs<T>(request.Completion.Task);
    }

    private static void EnsureReady()
    {
        if (!Dispatcher.HasOwner)
        {
            throw new InvalidOperationException(
                "The asset pipeline is not running; start the engine before loading assets."
            );
        }

        if (!Resolver.IsInitialized)
        {
            throw new InvalidOperationException(
                "The asset pipeline is not initialized; the content bootstrap must complete before loading assets."
            );
        }

        if (Volatile.Read(ref _loadsAccepted) == 0)
        {
            throw new InvalidOperationException(
                "The asset pipeline is stopping; new load requests are not accepted."
            );
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
            $"Asset loader for '{typeof(T).FullName}' returned '{asset.GetType().FullName}'."
        );
    }
}
