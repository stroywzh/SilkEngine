namespace SilkEngine.Asset;

/// <summary>Asset identity: asset table key, stable across frames.</summary>
public readonly record struct AssetId(ulong Value)
{
    /// <summary>Invalid id (0).</summary>
    public static AssetId Invalid => default;

    /// <summary>Whether the id is valid.</summary>
    public bool IsValid => Value != 0;
}
