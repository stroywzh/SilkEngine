namespace SilkEngine.Asset;

/// <summary>Persistent reference to an asset, stored by scenes and prefabs instead of a runtime identity.</summary>
internal readonly record struct AssetRef(AssetGuid Guid)
{
    /// <summary>Reference that does not resolve to any asset.</summary>
    public static AssetRef Invalid => default;
}
