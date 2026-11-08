using SilkEngine.Asset.Abstractions;
using SilkEngine.Core.AssetSystem;

namespace SilkEngine;

/// <summary>Business asset API: synchronous or asynchronous load by source path.</summary>
/// <remarks>Loaded assets are cached by the engine. Callers do not release them; the engine's scene lifecycle
/// (not yet implemented) owns cache-pin release, while the render side manages device bindings internally.</remarks>
public static class Assets
{
    /// <summary>Loads an asset synchronously; on the owner thread it decodes inline, on the host thread it
    /// submits and blocks until the owner registers the asset.</summary>
    /// <exception cref="InvalidOperationException">The pipeline is not running/initialized, or the caller is the
    /// render thread.</exception>
    /// <exception cref="AssetNotImportedException">The catalog has no entry for the path.</exception>
    public static T Load<T>(string sourcePath)
        where T : IAsset => AssetRuntime.Load<T>(sourcePath);

    /// <summary>Queues an asynchronous load; the task completes on the main-thread pump.</summary>
    /// <returns>Task completed with the cached asset, or faulted/cancelled with the failure.</returns>
    public static Task<T> LoadAsync<T>(
        string sourcePath,
        CancellationToken cancellationToken = default
    )
        where T : IAsset => AssetRuntime.LoadAsync<T>(sourcePath, cancellationToken);
}

/// <summary>Source file not imported: the sidecar next to the source is missing.</summary>
public sealed class AssetNotImportedException : IOException
{
    /// <summary>Creates the exception.</summary>
    public AssetNotImportedException(string message)
        : base(message) { }
}
