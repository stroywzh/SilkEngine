using System.Diagnostics.CodeAnalysis;
using System.IO;
using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;
using SilkEngine.Runtime;
using SilkEngine.Runtime.Host;

namespace SilkEngine.Core.AssetSystem.Runtime;

/// <summary>Internal composition root for the asset pipeline; the public facade forwards here.</summary>
/// <remarks>The engine main loop is the single owner of <see cref="AssetStore"/>: it drains commands, registers
/// decoded drafts, publishes snapshots and processes releases. Decoding itself runs on the thread pool.</remarks>
internal static class AssetRuntime
{
    private const byte LoadUpaccepted = 0;
    private const byte LoadAccepted = 1;
    private static int _loadsAccepted;
    private static AssetPump? _pump;

    internal static AssetDispatcher Dispatcher { get; } = new();

    internal static AssetResolver Resolver { get; } = new();

    internal static AssetBootstrapHost Bootstrap { get; } = new();

    internal static AssetRuntimeStore Store { get; } =
        new(Dispatcher.Ready, Dispatcher.Released, Dispatcher.PinReleased);

    /// <summary>The main-loop pump currently driving owner-thread work, if the engine is running.</summary>
    internal static AssetPump Pump =>
        Volatile.Read(ref _pump) ?? throw new NullReferenceException("Asset Pump not init yet.");

    internal static void AttachPump(AssetPump pump) => Volatile.Write(ref _pump, pump);

    /// <summary>Attaches the read-only catalog used to resolve source paths and persistent identities.</summary>
    internal static void AttachCatalog(IAssetCatalog catalog) => Resolver.AttachCatalog(catalog);

    internal static void Initialize()
    {
        Resolver.Initialize();
        RequestStart();

        //Run bootstarup at last
        Bootstrap.Run();
    }

    private static void RequestStart() => Volatile.Write(ref _loadsAccepted, LoadAccepted);

    internal static void RequestStop() => Volatile.Write(ref _loadsAccepted, LoadUpaccepted);

    internal static void StopCore()
    {
        Store.ReleaseAllRetention();
        Store.Commit();

        // Dispatcher.DetachOwner();

        Volatile.Write(ref _pump, null);
    }

    /// <summary>Loads synchronously; the store retention keeps the asset alive until it is unloaded.</summary>
    internal static T Load<T>(string sourcePath)
        where T : IAsset
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        EnsureReady();

        IAssetLoader loader = Resolver.ResolveLoader(typeof(T));
        AssetSource source = Resolver.ResolveSource(sourcePath);
        return LoadSyncCore<T>(source, loader);
    }

    /// <summary>Queues an asynchronous load; the store retention keeps the asset alive until it is unloaded.</summary>
    internal static async Task<T> LoadAsync<T>(
        string sourcePath,
        CancellationToken cancellationToken = default
    )
        where T : IAsset
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        EnsureReady();

        IAssetLoader loader = Resolver.ResolveLoader(typeof(T));
        AssetSource source = Resolver.ResolveSource(sourcePath);
        return await LoadAsyncCore<T>(source, loader, cancellationToken);
    }

    // Synchronous load path; the returned asset is kept alive by the store retention.
    private static T LoadSyncCore<T>(
        AssetSource source,
        IAssetLoader loader,
        CancellationToken token = default
    )
        where T : IAsset
    {
        if (ThreadRegistry.IsRenderThread)
        {
            throw new InvalidOperationException(
                "Assets.Load<T> must not run on the render thread; use LoadAsync<T> or the render bridge."
            );
        }

        var request = new AssetLoadRequest(
            source,
            loader,
            (token != default) ? token : CancellationToken.None,
            blocking: true
        );

        if (Dispatcher.IsOwnerThread)
        {
            // Route an owner-thread blocking load through the same pump so it joins an in-flight decode for
            // the same path instead of decoding the payload a second time. The owner must drive the pump
            // itself while it blocks; the main loop is not available for that.
            AssetPump pump = Pump ?? throw new NullReferenceException();
            if (pump is null || pump.IsPumping || pump.IsStopped)
            {
                return Cast<T>(Store.LoadSync(source, loader));
            }

            Dispatcher.Submit(AssetCommand.Load(request));

            while (!request.Completion.Task.IsCompleted)
            {
                if (pump.IsStopped)
                    break;

                pump.Pump();

                // SpinWait 和 轮询中if下ThreadSleep 到时候再说吧
                SpinWait.SpinUntil(() =>
                {
                    return request.Completion.Task.IsCompleted;
                });

                // if (!request.Completion.Task.IsCompleted)
                // {
                //     Thread.Sleep(1);
                // }
            }

            return Cast<T>(request.Completion.Task.GetAwaiter().GetResult());
        }

        Dispatcher.Submit(AssetCommand.Load(request));
        IAsset asset = request.Completion.Task.GetAwaiter().GetResult();
        return Cast<T>(asset);
    }

    private static Task<T> LoadAsyncCore<T>(
        AssetSource source,
        IAssetLoader loader,
        CancellationToken cancellationToken
    )
        where T : IAsset
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromCanceled<T>(cancellationToken);

        var request = new AssetLoadRequest(source, loader, cancellationToken, blocking: false);
        Dispatcher.Submit(AssetCommand.Load(request));

        return AwaitAs<T>(request.Completion.Task);
    }

    internal static bool TryGet<T>(AssetId assetId, out T asset)
        where T : IAsset
    {
        if (Store.TryGet(assetId, out AssetView view) && view.Asset is T typed)
        {
            asset = typed;
            return true;
        }

        asset = default!;
        return false;
    }

    /// <summary>Releases the store retention for one asset; the scene lifecycle (not implemented yet) calls this.</summary>
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

        Dispatcher.Submit(AssetCommand.Unload(assetId));
        return true;
    }

    /// <summary>Loads an asset and returns a caller ticket for it.</summary>
    internal static AssetHandle<T> LoadHandle<T>(string sourcePath)
        where T : IAsset
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        EnsureReady();

        IAssetLoader loader = Resolver.ResolveLoader(typeof(T));
        AssetSource source = Resolver.ResolveSource(sourcePath);
        return AcquireTicket<T>(LoadSyncCore<T>(source, loader));
    }

    /// <summary>Queues an async load and returns a caller ticket for the asset.</summary>
    internal static async Task<AssetHandle<T>> LoadHandleAsync<T>(
        string sourcePath,
        CancellationToken cancellationToken
    )
        where T : IAsset
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        EnsureReady();

        IAssetLoader loader = Resolver.ResolveLoader(typeof(T));
        AssetSource source = Resolver.ResolveSource(sourcePath);
        T asset = await LoadAsyncCore<T>(source, loader, cancellationToken).ConfigureAwait(false);
        return AcquireTicket<T>(asset);
    }

    // Takes a caller ticket on a freshly loaded asset; the store retention stays independent. Uses the asset's
    // own handle instead of the published snapshot, which a concurrent commit may not have refreshed yet.
    private static AssetHandle<T> AcquireTicket<T>(T asset)
        where T : IAsset
    {
        if (asset is AssetBase assetBase && assetBase.Handle is { IsValid: true } retention)
        {
            if (!retention.Lifetime.AddTicketCount())
                throw new InvalidOperationException(
                    $"Asset {asset.AssetId.Value} is no longer alive."
                );

            return new AssetHandle<T>(asset.AssetId, asset, retention.Lifetime);
        }

        return TryAcquireHandle<T>(asset.AssetId)
            ?? throw new InvalidOperationException(
                $"Asset {asset.AssetId.Value} is no longer alive."
            );
    }

    /// <summary>Takes one caller ticket on a loaded asset; returns null when the record is gone.</summary>
    internal static AssetHandle<T>? TryAcquireHandle<T>(AssetId assetId)
        where T : IAsset
    {
        if (!Store.TryGet(assetId, out AssetView view) || view.Asset is not T typed)
            return null;

        if (!view.Lifetime.AddTicketCount())
            return null;

        return new AssetHandle<T>(assetId, typed, view.Lifetime);
    }

    /// <summary>Takes one caller ticket for an existing handle; throws when the record is gone.</summary>
    internal static void AcquireHandleRef(AssetId assetId, AssetLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(lifetime);

        if (!lifetime.AddTicketCount())
            throw new InvalidOperationException($"Asset {assetId.Value} is no longer alive.");
    }

    /// <summary>Releases one caller ticket held by a handle; safe to call from any thread.</summary>
    internal static void ReleaseHandle(AssetId assetId, AssetLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(lifetime);

        lifetime.ReleaseTicket();
    }

    /// <summary>Releases the retention carried by a store handle; internal unload and shutdown only.</summary>
    internal static void ReleaseRetention(AssetId assetId, AssetLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(lifetime);

        lifetime.ReleaseRetention();
    }

    private static void EnsureReady()
    {
        // if (!Dispatcher.HasOwner)
        // {
        //     throw new InvalidOperationException(
        //         "The asset pipeline is not running; start the engine before loading assets."
        //     );
        // }

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
