using SilkEngine.Asset;
using SilkEngine.AssetSystem.Content.Abstractions;

namespace SilkEngine.AssetSystem.Content;

/// <summary>Import result: sidecar path, metadata, whether anything was written and recoverable warnings.</summary>
internal sealed record AssetImportResult(
    string SidecarPath,
    AssetSidecarBase Metadata,
    bool Changed,
    IReadOnlyList<string>? Warnings = null
)
{
    public override string ToString() =>
        $"{Metadata.AssetType} '{System.IO.Path.GetFileName(SidecarPath)}' changed={Changed}"
        + (Warnings is { Count: > 0 } ? $" warnings={Warnings.Count}" : string.Empty);
}
