using SilkEngine.Asset;
using SilkEngine.AssetSystem.Content.Abstractions;

namespace SilkEngine.AssetSystem.Content;

/// <summary>Import result: sidecar path, metadata and whether anything was written.</summary>
internal sealed record AssetImportResult(
    string SidecarPath,
    AssetSidecarBase Metadata,
    bool Changed
);
