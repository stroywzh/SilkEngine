using System.IO;
using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Core.AssetSystem;

/// <summary>Catalog and loader resolution for load requests; owns no lifecycle or refcount state.</summary>
/// <remarks>The catalog is attached once during startup. Loaders are frozen by <see cref="InitializeLoaders"/>
/// after the content bootstrap registered every loader, so an early load cannot cache an empty table.</remarks>
internal sealed class AssetResolver
{
    private Dictionary<Type, IAssetLoader> _loaders = new();
    private int _initialized;
    private IAssetCatalog? _catalog;

    internal bool IsInitialized => Volatile.Read(ref _initialized) != 0;

    /// <summary>Attaches the read-only catalog used to resolve source paths and persistent identities.</summary>
    internal void AttachCatalog(IAssetCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Volatile.Write(ref _catalog, catalog);
    }

    /// <summary>Freezes the loader map after the startup registration phase.</summary>
    internal void InitializeLoaders()
    {
        _loaders = new Dictionary<Type, IAssetLoader>(Application.AssetLoaders);
        Volatile.Write(ref _initialized, 1);
    }

    /// <summary>Resolves the loader registered for <paramref name="assetType"/>.</summary>
    internal IAssetLoader ResolveLoader(Type assetType)
    {
        if (Volatile.Read(ref _initialized) == 0)
        {
            throw new InvalidOperationException(
                "The asset pipeline is not initialized; the content bootstrap must complete before loading assets."
            );
        }

        if (_loaders.TryGetValue(assetType, out IAssetLoader? loader))
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

    private IAssetCatalog RequireCatalog() =>
        Volatile.Read(ref _catalog)
        ?? throw new InvalidOperationException(
            "No asset catalog is attached; the content bootstrap must attach one before loading."
        );

    private static string NormalizePath(string sourcePath) => Path.GetFullPath(sourcePath);
}