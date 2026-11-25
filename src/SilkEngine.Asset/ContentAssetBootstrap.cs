using System.Diagnostics;
using SilkEngine.Asset;
using SilkEngine.AssetSystem.Content.Abstractions;
using SilkEngine.AssetSystem.Runtime;

namespace SilkEngine.AssetSystem.Content;

/// <summary>Imports the project's content, builds the catalog, and attaches it to the asset system.</summary>
/// <remarks>Runs on the main thread as the configured <see cref="IAssetBootstrap"/>: importer and catalog
/// registrations happen here, so the application never touches the content pipeline.</remarks>
internal sealed class ContentAssetBootstrap : IAssetBootstrap
{
    public void Run(AssetStartupContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(context.ProjectRoot);

        bool strict = context.Mode == AssetStartupMode.Strict;
        Stopwatch stopwatch = Stopwatch.StartNew();

        AssetImporterRegistry.RegisterDefaults();

        ContentLayout layout = ContentLayout.Create(context.ProjectRoot);
        Log.Info($"Asset startup: importing '{layout.ProjectRoot}' ({context.Mode}).");

        var importWatch = Stopwatch.StartNew();
        IReadOnlyList<AssetImportResult> imported = AssetScanner.ImportAll(layout, strict);
        importWatch.Stop();
        Log.Info(
            $"Imported {imported.Count} asset(s) in {importWatch.Elapsed.TotalMilliseconds:0.0} ms."
        );

        AssetCatalog catalog;
        try
        {
            catalog = AssetCatalog.Build(layout, strict);
        }
        catch (Exception error) when (!strict)
        {
            Log.Exception(error);
            Log.Warning(
                "Asset catalog unavailable; loads by path will fail until the catalog can be built."
            );
            return;
        }

        // Attach before saving: a failed index write must not make the in-memory catalog unusable.
        AssetSystemHost.AttachCatalog(catalog);

        try
        {
            catalog.Save();
        }
        catch (Exception error) when (!strict)
        {
            Log.Exception(error);
            Log.Warning(
                $"Asset catalog save failed for '{layout.CatalogPath}'; loads still work from memory."
            );
        }

        Log.Info(
            $"Catalog: {catalog.Count} asset(s) in {stopwatch.Elapsed.TotalMilliseconds:0} ms."
        );
    }
}
