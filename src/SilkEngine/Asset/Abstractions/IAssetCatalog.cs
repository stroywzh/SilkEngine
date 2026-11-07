using System.Diagnostics.CodeAnalysis;

namespace SilkEngine.Asset.Abstractions;

/// <summary>Read-only index over the project's imported assets.</summary>
internal interface IAssetCatalog
{
    /// <summary>Number of indexed assets.</summary>
    int Count { get; }

    /// <summary>Finds an asset by persistent identity.</summary>
    bool TryGet(AssetGuid guid, [MaybeNullWhen(false)] out AssetCatalogEntry entry);

    /// <summary>Finds an asset by absolute or project-relative source path.</summary>
    bool TryGet(string sourcePath, [MaybeNullWhen(false)] out AssetCatalogEntry entry);

    /// <summary>Resolves everything a loader needs for the asset with the given identity.</summary>
    bool TryGetSource(AssetGuid guid, out AssetSource source);

    /// <summary>Resolves everything a loader needs for the asset at the given source path.</summary>
    bool TryGetSource(string sourcePath, out AssetSource source);
}
