using System.Diagnostics.CodeAnalysis;
using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;
using SilkEngine.Core.AssetSystem;

namespace SilkEngine;

/// <summary>Business asset API: asynchronous loading, queries, refcounting and ready/release notifications.</summary>
/// <remarks>The catalog that resolves a source path is attached during startup by the content bootstrap;
/// the engine host does that before <see cref="EngineHost.Start(EngineOptions?)"/> returns.</remarks>
public static class Assets
{
    /// <summary>Attaches the read-only catalog used to resolve source paths and persistent identities.</summary>
    /// <remarks>Called by the content bootstrap on the asset thread; callers do not attach catalogs themselves.</remarks>
    internal static void AttachCatalog(IAssetCatalog catalog) => AssetRuntime.AttachCatalog(catalog);

    /// <summary>Loads an asset synchronously by source path; valid only on the asset thread.</summary>
    /// <remarks>A catalog must be attached during startup before any load.</remarks>
    /// <exception cref="AssetNotImportedException">The catalog has no entry for the path.</exception>
    public static T Load<T>(string sourcePath)
        where T : IAsset => AssetRuntime.Load<T>(sourcePath);

    /// <summary>Queues an asynchronous load by source path.</summary>
    /// <remarks>A catalog must be attached during startup before any load.</remarks>
    /// <returns>Task completed on the main thread with the loaded asset, or faulted/cancelled with the failure.</returns>
    public static Task<T> LoadAsync<T>(
        string sourcePath,
        CancellationToken cancellationToken = default
    )
        where T : IAsset => AssetRuntime.LoadAsync<T>(sourcePath, cancellationToken);

    /// <summary>Queries a loaded asset by asset id; readable from any thread through the published snapshot.</summary>
    public static bool TryGet<T>(AssetId assetId, [MaybeNullWhen(false)] out T asset)
        where T : IAsset => AssetRuntime.TryGet(assetId, out asset);

    /// <summary>Releases one loaded asset instance: the refcount reaches zero and enters the release window.</summary>
    /// <remarks>The worker applies the decrement; the render thread keeps drawing the asset until the release
    /// notification arrives.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="asset"/> is <c>null</c>.</exception>
    public static void Release(IAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        AssetRuntime.Release(asset.AssetId);
    }

    /// <summary>Queries a loaded asset by source path.</summary>
    internal static bool TryGet<T>(string sourcePath, out T? asset)
        where T : IAsset => AssetRuntime.TryGet(sourcePath, out asset);

    /// <summary>Queries the published runtime record (asset plus payload lease) by asset id.</summary>
    internal static bool TryGetView(AssetId assetId, out AssetView view) =>
        AssetRuntime.TryGetView(assetId, out view);

    /// <summary>Takes one ready id (render thread).</summary>
    internal static bool TryTakeReady(out AssetId assetId) => AssetRuntime.TryTakeReady(out assetId);

    /// <summary>Takes one released id (render thread).</summary>
    internal static bool TryTakeReleased(out AssetId assetId) =>
        AssetRuntime.TryTakeReleased(out assetId);

    /// <summary>Resolves the loaded asset for a runtime binding (render bridge; no runtime view leaks out).</summary>
    internal static bool TryGetRenderAsset(
        AssetId assetId,
        [MaybeNullWhen(false)] out IAsset asset
    ) => AssetRuntime.TryGetRenderAsset(assetId, out asset);

    /// <summary>Takes the decoded payload of a loaded asset for upload (render bridge, one-shot).</summary>
    internal static bool TryTakePayload<T>(AssetId assetId, [MaybeNullWhen(false)] out T payload)
        where T : class, IAssetPayload => AssetRuntime.TryTakePayload(assetId, out payload);

    /// <summary>Queued requests plus requests currently decoding on the thread pool.</summary>
    internal static int PendingCount => AssetRuntime.PendingCount;

    /// <summary>Assets inside their release window plus released ids the render thread has not taken yet.</summary>
    internal static int PendingReleaseCount => AssetRuntime.PendingReleaseCount;
}

/// <summary>Source file not imported: the sidecar next to the source is missing.</summary>
public sealed class AssetNotImportedException : IOException
{
    /// <summary>Creates the exception.</summary>
    public AssetNotImportedException(string message)
        : base(message) { }
}
