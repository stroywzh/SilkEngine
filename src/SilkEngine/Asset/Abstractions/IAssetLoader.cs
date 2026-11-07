namespace SilkEngine.Asset.Abstractions;

/// <summary>Asset loader: converts a resolved source into decoded content.</summary>
/// <remarks>Called on a thread-pool thread, so implementations must not touch the asset store, <c>Assets</c>
/// or any other shared mutable state; registering the payload and binding the asset id is the engine's job.
/// Loaders only read the resolved source it is given: they must not register anything or fall back to
/// path conventions to find their inputs.</remarks>
internal interface IAssetLoader
{
    /// <summary>Decodes the resolved source into an unbound asset plus its CPU payload.</summary>
    /// <param name="source">Resolved project path, sidecar path, artifact path and header JSON.</param>
    /// <returns>Draft handed back to the engine for registration.</returns>
    /// <exception cref="AssetNotImportedException">Source file has not been imported (sidecar missing).</exception>
    /// <exception cref="System.IO.InvalidDataException">Sidecar or source payload is invalid.</exception>
    AssetDraft Load(in AssetSource source);
}
