using System.IO;
using SilkEngine.Asset.Abstractions;
using SilkEngine.Core.AssetSystem;

namespace SilkEngine.Asset;

/// <summary>Business asset API: asynchronous loading, queries, refcounting and ready/release notifications.</summary>
public static class Assets
{
    /// <summary>Loads an asset synchronously.</summary>
    /// <exception cref="AssetNotImportedException">Source file has not been imported (sidecar missing).</exception>
    public static T Load<T>(string sourcePath)
        where T : IAsset => AssetSystem.Load<T>(sourcePath);

    /// <summary>Queues an asynchronous load.</summary>
    /// <returns>Task completed on the main thread with the loaded asset, or faulted/cancelled with the failure.</returns>
    public static Task<T> LoadAsync<T>(
        string sourcePath,
        CancellationToken cancellationToken = default
    )
        where T : IAsset => AssetSystem.LoadAsync<T>(sourcePath, cancellationToken);

    /// <summary>Queries a loaded asset by asset id; readable from any thread through the published snapshot.</summary>
    public static bool TryGet<T>(AssetId assetId, out T? asset)
        where T : IAsset => AssetSystem.TryGet(assetId, out asset);

    /// <summary>Queries a loaded asset by source path.</summary>
    public static bool TryGet<T>(string sourcePath, out T? asset)
        where T : IAsset => AssetSystem.TryGet(sourcePath, out asset);

    /// <summary>Takes one ready id (render thread).</summary>
    public static bool TryTakeReady(out AssetId assetId) => AssetSystem.TryTakeReady(out assetId);

    /// <summary>Takes one released id (render thread).</summary>
    public static bool TryTakeReleased(out AssetId assetId) =>
        AssetSystem.TryTakeReleased(out assetId);

    /// <summary>Decrements the refcount; zero enters the release window (released immediately in inline mode).</summary>
    /// <remarks>The worker applies the decrement; the render thread keeps drawing the asset until the release
    /// notification arrives.</remarks>
    public static void Unload(AssetId assetId) => AssetSystem.Unload(assetId);

    /// <summary>Queued requests plus requests currently decoding on the thread pool.</summary>
    public static int PendingCount => AssetSystem.PendingCount;

    /// <summary>Assets inside their release window plus released ids the render thread has not taken yet.</summary>
    public static int PendingReleaseCount => AssetSystem.PendingReleaseCount;
}

/// <summary>Source file not imported: the sidecar next to the source is missing.</summary>
public sealed class AssetNotImportedException : IOException
{
    /// <summary>Creates the exception.</summary>
    public AssetNotImportedException(string message)
        : base(message) { }
}
