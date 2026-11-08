namespace SilkEngine.Asset.Abstractions;

/// <summary>Builds one catalog entry from a source file and its sidecar.</summary>
/// <remarks>Registered by source extension so the catalog does not hardcode any concrete sidecar type.</remarks>
internal interface IAssetCatalogAdapter
{
    /// <summary>Source extension handled by this adapter, including the leading dot.</summary>
    string SourceExtension { get; }

    /// <summary>Reads the sidecar/artifact and produces the catalog entry; false skips the source.</summary>
    bool TryCreateEntry(
        ContentLayout layout,
        string sourcePath,
        out AssetCatalogEntry entry
    );
}
