namespace SilkEngine.Asset;

/// <summary>Marker interface for runtime assets.</summary>
public interface IAsset
{
    /// <summary>Asset identity.</summary>
    AssetId AssetId { get; }
}
