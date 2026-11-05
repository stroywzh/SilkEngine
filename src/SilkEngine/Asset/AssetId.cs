namespace SilkEngine.Asset;

/// <summary>Asset identity: asset table key, stable across frames.</summary>
public readonly record struct AssetId(ulong Value)
{
    public static AssetId Invalid => default;
    public bool IsValid => Value != 0;
}
