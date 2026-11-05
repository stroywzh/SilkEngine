namespace SilkEngine.Asset.Abstractions;

/// <summary>Asset loader: converts a source path into decoded content.</summary>
/// <remarks>Called on a thread-pool thread, so implementations must not touch <c>AssetTable</c>, <c>Assets</c>
/// or any other shared mutable state; registering the payload and binding the asset id is the engine's job.</remarks>
public interface IAssetLoader
{
    /// <summary>Decodes a source file into an unbound asset plus its CPU payload.</summary>
    /// <returns>Draft handed back to the engine for registration.</returns>
    /// <exception cref="AssetNotImportedException">Source file has not been imported (sidecar missing).</exception>
    /// <exception cref="System.IO.InvalidDataException">Sidecar or source payload is invalid.</exception>
    AssetDraft Load(string sourcePath);
}
