using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Asset;

public abstract record AssetBase : IEquatable<AssetBase>, IAsset
{
    protected AssetBase() { }

    protected AssetBase(AssetId assetId) => AssetId = assetId;

    public AssetId AssetId { get; init; }

    public virtual bool Equals(AssetBase? other) =>
        other is not null && other.AssetId != AssetId.Invalid && other.AssetId == AssetId;

    public override int GetHashCode() => AssetId.GetHashCode();
}
