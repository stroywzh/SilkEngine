namespace SilkEngine.Asset;

/// <summary>Import result: sidecar path, metadata and whether anything was written.</summary>
public sealed record AssetImportResult(string SidecarPath, AssetSidecarBase Metadata, bool Changed);
