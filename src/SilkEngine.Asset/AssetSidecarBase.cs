namespace SilkEngine.Asset;

/// <summary>
/// Base abstract class for evety AssetSidecar, only data contained.
/// </summary>
internal abstract class AssetSidecarBase
{
    /// <summary>Schema version read from the document; defaults to the current version for new records.</summary>
    public abstract int SchemaVersion { get; init; }
    public abstract string AssetType { get; }
    public string Source { get; init; } = string.Empty;
    public string SourceHash { get; init; } = string.Empty;
}
