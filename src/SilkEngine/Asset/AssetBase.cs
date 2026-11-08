using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Asset;

/// <summary>Base type for engine runtime assets: identity is <see cref="AssetId"/>, never value equality.</summary>
/// <remarks>A plain class keeps derived assets from synthesizing record value equality/hash over mutable fields
/// (arrays, descriptors). <see cref="Equals(AssetBase?)"/> is reflexive even for <see cref="AssetId.Invalid"/>.</remarks>
public abstract class AssetBase : IEquatable<AssetBase>, IAsset
{
    protected AssetBase() { }

    protected AssetBase(AssetId assetId) => AssetId = assetId;

    public AssetId AssetId { get; init; }

    public bool Equals(AssetBase? other) =>
        ReferenceEquals(this, other)
        || (other is not null && AssetId.IsValid && AssetId == other.AssetId);

    public override bool Equals(object? obj) => Equals(obj as AssetBase);

    public override int GetHashCode() => AssetId.GetHashCode();
}
