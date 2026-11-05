namespace SilkEngine.Asset;

/// <summary>所有 sidecar 的纯数据基类：无 IO、无序列化。</summary>
public abstract class AssetSidecarBase
{
    public abstract int SchemaVersion { get; }
    public abstract string AssetType { get; }
    public string Source { get; init; } = string.Empty;
    public string SourceHash { get; init; } = string.Empty;
}
