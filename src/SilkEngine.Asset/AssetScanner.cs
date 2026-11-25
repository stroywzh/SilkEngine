using System.Diagnostics;
using SilkEngine.Asset;
using SilkEngine.AssetSystem.Content.Abstractions;

namespace SilkEngine.AssetSystem.Content;

/// <summary>One source file under <c>Assets/</c> plus the importer registered for its extension.</summary>
internal readonly record struct DiscoveredAsset(string SourcePath, IAssetImporter Importer);

/// <summary>Finds and imports content under a project's <c>Assets/</c> folder using the importer registry.</summary>
/// <remarks>Every file whose extension has a registered importer is content; file names are never special-cased.</remarks>
internal static class AssetScanner
{
    /// <summary>Returns every source under the layout's <c>Assets/</c> whose extension has a registered importer.</summary>
    /// <remarks>Returns empty when the folder does not exist. Importers must be registered before scanning.</remarks>
    public static IReadOnlyList<DiscoveredAsset> Scan(ContentLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var discovered = new List<DiscoveredAsset>();
        if (!Directory.Exists(layout.AssetsRoot))
            return discovered;

        foreach (
            string sourcePath in Directory.EnumerateFiles(
                layout.AssetsRoot,
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

    /// <summary>Convenience overload for tools/tests: derives the layout from a project root.</summary>
    public static IReadOnlyList<DiscoveredAsset> Scan(string projectRoot) =>
        Scan(ContentLayout.Create(projectRoot));

    /// <summary>Imports every discovered source, skipping (and logging) the ones that fail.</summary>
    /// <returns>Results of the successful imports only.</returns>
    internal static IReadOnlyList<AssetImportResult> ImportAll(ContentLayout layout) =>
        ImportAll(layout, strict: false);

    /// <summary>Convenience overload for tools/tests: derives the layout from a project root.</summary>
    internal static IReadOnlyList<AssetImportResult> ImportAll(
        string projectRoot,
        bool strict = false
    ) => ImportAll(ContentLayout.Create(projectRoot), strict);

    /// <summary>Imports every discovered source; in strict mode any scan or import failure aborts startup.</summary>
    /// <returns>Results of the successful imports only.</returns>
    internal static IReadOnlyList<AssetImportResult> ImportAll(ContentLayout layout, bool strict)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var results = new List<AssetImportResult>();

        IReadOnlyList<DiscoveredAsset> discovered;
        try
        {
            discovered = Scan(layout);
        }
        catch (Exception error) when (!strict)
        {
            Log.Exception(error);
            return results;
        }

        var failures = new List<Exception>();
        var pass = Stopwatch.StartNew();
        foreach (DiscoveredAsset asset in discovered)
        {
            var import = Stopwatch.StartNew();
            try
            {
                AssetImportResult result = asset.Importer.Import(asset.SourcePath, layout);
                import.Stop();
                ReportImport(asset, result, import.Elapsed, error: null);
                results.Add(result);
            }
            catch (Exception error)
            {
                import.Stop();
                // One bad source must not stop the rest; the render side falls back for that asset.
                ReportImport(asset, result: null, import.Elapsed, error);
                failures.Add(error);
            }
        }
        pass.Stop();

        int withWarnings = results.Count(static result => result.Warnings is { Count: > 0 });
        string summary =
            $"Import pass finished in {pass.Elapsed.TotalMilliseconds:0.0} ms: "
            + $"{results.Count} succeeded ({withWarnings} with warnings), {failures.Count} failed.";

        if (failures.Count > 0)
        {
            Log.Warning(summary);
        }
        else
        {
            Log.Info(summary);
        }

        if (strict && failures.Count > 0)
        {
            throw new AggregateException(
                $"Asset import failed for {failures.Count} source(s) under '{layout.ProjectRoot}'.",
                failures
            );
        }

        return results;
    }

    private static void ReportImport(
        DiscoveredAsset asset,
        AssetImportResult? result,
        TimeSpan elapsed,
        Exception? error
    )
    {
        string extension = asset.Importer.Extension.TrimStart('.');
        string type =
            result is null ? extension : $"{result.Metadata.AssetType}/{extension}";
        string duration = $"{elapsed.TotalMilliseconds:0.0} ms";

        if (error is not null)
        {
            Log.Error($"Import failed [{type}] '{asset.SourcePath}' in {duration}:\n    - Error: {error}");
            return;
        }

        string state = result!.Changed ? "succeeded" : "up to date";
        string details = result.Metadata.ToString() ?? result.Metadata.AssetType;

        if (result.Warnings is { Count: > 0 } warnings)
        {
            Log.Warning(
                $"Import {state} with warnings [{type}] '{asset.SourcePath}' in {duration}: "
                    + $"{details}; {string.Join("; ", warnings)}"
            );
            return;
        }

        Log.Info($"Import {state} [{type}] '{asset.SourcePath}' in {duration}: {details}");
    }
}
