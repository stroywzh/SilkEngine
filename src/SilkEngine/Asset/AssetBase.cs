using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Asset;

/// <summary>Base type for engine runtime assets: identity is <c>AssetId</c>, never value equality.</summary>
/// <remarks>A plain class keeps derived assets from synthesizing record value equality/hash over mutable fields
/// (arrays, descriptors). Every instance is created with its store-issued handle, so <see cref="AssetId"/> is
/// always valid; <c>AssetId.Invalid</c> only exists as <c>default(AssetId)</c>. <see cref="Equals(AssetBase?)"/>
/// is reflexive for every instance.</remarks>
public abstract class AssetBase : IEquatable<AssetBase>, IAsset
{
    private AssetHandle _handle;

    /// <summary>Content-side catalog type name of this runtime asset.</summary>
    public abstract string AssetTypeName { get; }

    /// <summary>Creates an asset bound to its store-issued handle.</summary>
    protected AssetBase(AssetHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        _handle = handle;
    }

    /// <summary>Identity of this asset, taken from the handle that keeps it alive.</summary>
    public AssetId AssetId => Volatile.Read(ref _handle).Id;

    /// <summary>Handle that keeps this asset alive.</summary>
    /// <remarks>Set by the loader path and replaced when a released record is revived; dispose it only through
    /// the internal unload path.</remarks>
    internal AssetHandle Handle => Volatile.Read(ref _handle);

    /// <summary>Replaces the handle that keeps this asset alive.</summary>
    internal void AttachHandle(AssetHandle handle) => Volatile.Write(ref _handle, handle);

    public bool Equals(AssetBase? other) =>
        ReferenceEquals(this, other)
        || (other is not null && AssetId.IsValid && AssetId == other.AssetId);

    public override bool Equals(object? obj) => Equals(obj as AssetBase);

    public override int GetHashCode() => AssetId.GetHashCode();
}
