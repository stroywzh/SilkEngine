using SilkEngine;

namespace SilkEngine.Core.AssetSystem;

/// <summary>Content work the asset worker performs once, before it processes any load request.</summary>
/// <remarks>Runs on the main thread as the store owner, so a bootstrap may attach the catalog and register
/// loaders/importers without extra synchronization. The engine host waits for it to finish.</remarks>
public interface IAssetBootstrap
{
    /// <summary>Runs startup work; throwing marks the bootstrap as failed.</summary>
    void Run(AssetStartupContext context);
}

/// <summary>Inputs for <see cref="IAssetBootstrap"/>: the project root whose <c>Assets/</c> folder is content.</summary>
public readonly record struct AssetStartupContext(string ProjectRoot, AssetStartupMode Mode);
