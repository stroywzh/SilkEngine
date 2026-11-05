namespace SilkEngine.Asset.Abstractions;

/// <summary>Marker interface for runtime assets.</summary>
public interface IAsset
{
    /// <summary>Asset identity.</summary>
    AssetId AssetId { get; }

    /// <summary>Returns a copy of this asset bound to <paramref name="assetId"/>.</summary>
    IAsset WithAssetId(AssetId assetId);
}
