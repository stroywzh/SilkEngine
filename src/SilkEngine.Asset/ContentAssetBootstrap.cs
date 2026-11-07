using System.Diagnostics;
using SilkEngine.Core.AssetSystem;

namespace SilkEngine.Asset;

/// <summary>Imports the project's content, builds the catalog, and attaches it to the asset system.</summary>
/// <remarks>Runs on the asset thread as the configured <see cref="IAssetBootstrap"/>: importer and loader
/// registrations happen here, so the application never touches the content pipeline.</remarks>
internal sealed class ContentAssetBootstrap : IAssetBootstrap
{
    public void Run(AssetStartupContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(context.ProjectRoot);

        bool strict = context.Mode == AssetStartupMode.Strict;
        Stopwatch stopwatch = Stopwatch.StartNew();

        AssetImporterRegistry.RegisterDefaults();
        AssetLoaderRegistration.RegisterDefaults();

        Log.Info($"Asset startup: importing '{context.ProjectRoot}' ({context.Mode}).");

        IReadOnlyList<AssetImportResult> imported = AssetScanner.ImportAll(
            context.ProjectRoot,
            strict
        );
        Log.Info($"Imported {imported.Count} asset(s).");
        foreach (AssetImportResult result in imported)
            Log.Info($"Imported '{result.SidecarPath}' (changed: {result.Changed}).");

        AssetCatalog catalog;
        try
        {
            catalog = AssetCatalog.Build(context.ProjectRoot);
            catalog.Save();
        }
        catch (Exception error) when (!strict)
        {
            Log.Exception(error);
            Log.Warning(
                "Asset catalog unavailable; loads by path will fail until the catalog can be built."
            );
            return;
        }

        Assets.AttachCatalog(catalog);
        Log.Info(
            $"Catalog: {catalog.Count} asset(s) in {stopwatch.Elapsed.TotalMilliseconds:0} ms."
        );
    }
}
