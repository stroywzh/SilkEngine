namespace SilkEngine.Asset;

public enum TextureFormat : byte
{
    RGBA32 = 1,
    // Future: BC7, ASTC_4x4, ...
}

public enum TextureColorSpace : byte
{
    Linear = 0,
    SRGB = 1,
}
