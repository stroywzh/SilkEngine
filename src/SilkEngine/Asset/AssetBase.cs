using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Asset;

/// <summary>Base type for engine runtime assets: identity is <c>AssetId</c>, never value equality.</summary>
/// <remarks>A plain class keeps derived assets from synthesizing record value equality/hash over mutable fields
/// (arrays, descriptors). An unbound descriptor reports <c>AssetId.Invalid</c> until it is attached to a
/// handle. <see cref="Equals(AssetBase?)"/> is reflexive even for <c>AssetId.Invalid</c>.</remarks>
public abstract class AssetBase : IEquatable<AssetBase>, IAsset
{
    private AssetHandle? _handle;

    /// <summary>Catalog type name shared with the content-side sidecar.</summary>
    internal const string TextureAssetName = "texture";
    internal const string MeshAssetName = "mesh";

    /// <summary>Content-side catalog type name of this runtime asset.</summary>
    public abstract string AssetTypeName { get; }

    /// <summary>Creates an unbound descriptor whose identity is assigned when the store registers it.</summary>
    protected AssetBase() { }

    /// <summary>Creates an asset bound to its store-issued handle.</summary>
    protected AssetBase(AssetHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        Volatile.Write(ref _handle, handle);
    }

    /// <summary>Identity of the registered asset; <c>AssetId.Invalid</c> while unbound.</summary>
    public AssetId AssetId => Volatile.Read(ref _handle)?.Id ?? default;

    /// <summary>Handle retained by the asset itself, or null for a descriptor created without one.</summary>
    /// <remarks>Set by the loader path and replaced when a released record is revived; dispose it only through
    /// the internal unload path.</remarks>
    internal AssetHandle? Handle => Volatile.Read(ref _handle);

    /// <summary>Attaches or replaces the handle that keeps this asset alive.</summary>
    internal void AttachHandle(AssetHandle handle) => Volatile.Write(ref _handle, handle);

    public bool Equals(AssetBase? other) =>
        ReferenceEquals(this, other)
        || (other is not null && AssetId.IsValid && AssetId == other.AssetId);

    public override bool Equals(object? obj) => Equals(obj as AssetBase);

    public override int GetHashCode() => AssetId.GetHashCode();
}
