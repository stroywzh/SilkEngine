using System.IO;
using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Core.AssetSystem;

/// <summary>Catalog and loader resolution for load requests; owns no lifecycle or refcount state.</summary>
/// <remarks>The catalog is attached once during startup. The loader map is frozen on first resolution, so the
/// content bootstrap must register every loader before the first load.</remarks>
internal sealed class AssetResolver
{
    private readonly Lazy<Dictionary<Type, IAssetLoader>> _loaders = new(static () =>
        new Dictionary<Type, IAssetLoader>(Application.AssetLoaders)
    );

    private IAssetCatalog? _catalog;

    /// <summary>Attaches the read-only catalog used to resolve source paths and persistent identities.</summary>
    internal void AttachCatalog(IAssetCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Volatile.Write(ref _catalog, catalog);
    }

    /// <summary>Resolves the loader registered for <paramref name="assetType"/>.</summary>
    internal IAssetLoader ResolveLoader(Type assetType)
    {
        if (_loaders.Value.TryGetValue(assetType, out IAssetLoader? loader))
            return loader;

        throw new NotSupportedException(
            $"No asset loader is registered for '{assetType.FullName}'. The content bootstrap must register it before startup."
        );
    }

    /// <summary>Resolves the source a loader reads for the given path.</summary>
    internal AssetSource ResolveSource(string sourcePath)
    {
        IAssetCatalog catalog = RequireCatalog();

        // The catalog resolves both project-relative ("Assets/Image.png") and absolute paths against the
        // project root; fall back to a working-directory absolute path for callers outside the project.
        if (catalog.TryGetSource(sourcePath, out AssetSource source))
            return source;

        string path = NormalizePath(sourcePath);
        if (catalog.TryGetSource(path, out source))
            return source;

        throw new AssetNotImportedException(
            $"Asset '{path}' is not in the asset catalog. Reimport it (or rebuild the catalog) and retry."
        );
    }

    /// <summary>Resolves the source a loader reads for the given persistent identity.</summary>
    internal AssetSource ResolveSource(AssetRef reference)
    {
        if (!reference.Guid.IsValid)
        {
            throw new AssetNotImportedException(
                $"Asset {nameof(AssetRef)} has no valid guid; it cannot be resolved."
            );
        }

        if (RequireCatalog().TryGetSource(reference.Guid, out AssetSource source))
            return source;

        throw new AssetNotImportedException(
            $"Asset '{reference.Guid}' is not in the asset catalog. Reimport it (or rebuild the catalog) and retry."
        );
    }

    private IAssetCatalog RequireCatalog() =>
        Volatile.Read(ref _catalog)
        ?? throw new InvalidOperationException(
            "No asset catalog is attached; the content bootstrap must attach one before loading."
        );

    private static string NormalizePath(string sourcePath) => Path.GetFullPath(sourcePath);
}
