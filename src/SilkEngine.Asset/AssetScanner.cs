using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Asset;

/// <summary>One source file under <c>Assets/</c> plus the importer registered for its extension.</summary>
internal readonly record struct DiscoveredAsset(string SourcePath, IAssetImporter Importer);

/// <summary>Finds and imports content under a project's <c>Assets/</c> folder using the importer registry.</summary>
/// <remarks>Every file whose extension has a registered importer is content; file names are never special-cased.</remarks>
internal static class AssetScanner
{
    /// <summary>Returns every source under <c>&lt;projectRoot&gt;/Assets</c> whose extension has a registered importer.</summary>
    /// <remarks>Returns empty when the folder does not exist. Importers must be registered before scanning.</remarks>
    public static IReadOnlyList<DiscoveredAsset> Scan(string projectRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        string assetsRoot = Path.Combine(
            Path.GetFullPath(projectRoot),
            AssetConstants.AssetsFolderName
        );
        var discovered = new List<DiscoveredAsset>();
        if (!Directory.Exists(assetsRoot))
            return discovered;

        foreach (
            string sourcePath in Directory.EnumerateFiles(
                assetsRoot,
                "*",
                SearchOption.AllDirectories
            )
        )
        {
            if (
                AssetImporterRegistry.TryGet(
                    Path.GetExtension(sourcePath),
                    out IAssetImporter importer
                )
            )
            {
                discovered.Add(new DiscoveredAsset(sourcePath, importer));
            }
        }

        return discovered;
    }

    /// <summary>Imports every discovered source, skipping (and logging) the ones that fail.</summary>
    /// <returns>Results of the successful imports only.</returns>
    internal static IReadOnlyList<AssetImportResult> ImportAll(string projectRoot) =>
        ImportAll(projectRoot, strict: false);

    /// <summary>Imports every discovered source; in strict mode any scan or import failure aborts startup.</summary>
    /// <remarks>Best-effort keeps the historical behavior: a bad source is logged and skipped, the rest import.
    /// Strict aggregates every failure and throws, so the engine host can stop with a clear error.</remarks>
    /// <returns>Results of the successful imports only.</returns>
    internal static IReadOnlyList<AssetImportResult> ImportAll(string projectRoot, bool strict)
    {
        var results = new List<AssetImportResult>();

        IReadOnlyList<DiscoveredAsset> discovered;
        try
        {
            discovered = Scan(projectRoot);
        }
        catch (Exception error) when (!strict)
        {
            Log.Exception(error);
            return results;
        }

        var failures = new List<Exception>();
        foreach (DiscoveredAsset asset in discovered)
        {
            try
            {
                results.Add(asset.Importer.Import(asset.SourcePath));
            }
            catch (Exception error)
            {
                // One bad source must not stop the rest; the render side falls back for that asset.
                Log.Exception(error);
                failures.Add(error);
            }
        }

        if (strict && failures.Count > 0)
        {
            throw new AggregateException(
                $"Asset import failed for {failures.Count} source(s) under '{projectRoot}'.",
                failures
            );
        }

        return results;
    }
}
