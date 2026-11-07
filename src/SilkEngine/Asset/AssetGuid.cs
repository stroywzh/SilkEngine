namespace SilkEngine.Asset;

/// <summary>Persistent asset identity: survives re-imports, setting changes and source file moves.</summary>
internal readonly record struct AssetGuid(Guid Value)
{
    /// <summary>No identity.</summary>
    public static AssetGuid Invalid => default;

    /// <summary>True when <see cref="Value"/> is not the empty GUID.</summary>
    public bool IsValid => Value != Guid.Empty;

    /// <summary>Creates a new random identity.</summary>
    public static AssetGuid New() => new(Guid.NewGuid());

    /// <summary>Canonical <c>N</c> representation.</summary>
    public override string ToString() => Value.ToString("N");

    /// <summary>Parses <c>N</c> or <c>D</c> text.</summary>
    public static bool TryParse(string? text, out AssetGuid guid)
    {
        if (Guid.TryParse(text, out Guid value))
        {
            guid = new AssetGuid(value);
            return true;
        }

        guid = default;
        return false;
    }
}
