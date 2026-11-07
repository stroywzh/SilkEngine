namespace SilkEngine.Asset;

/// <summary>Asset identity: asset store key, stable across frames.</summary>
public readonly record struct AssetId
{
    public readonly ulong Value;

    internal AssetId(ulong value)
    {
        this.Value = value;
    }

    public static AssetId Invalid => default;
    public bool IsValid => Value != 0;
}
