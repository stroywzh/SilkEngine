namespace SilkEngine.Asset;

/// <summary>Import result: sidecar path, metadata and whether anything was written.</summary>
internal sealed record AssetImportResult(string SidecarPath, AssetSidecarBase Metadata, bool Changed);
