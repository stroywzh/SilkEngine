namespace SilkEngine.Asset;

/// <summary>Pixel formats understood by the runtime texture asset and the render abstraction.</summary>
public enum TextureFormat : byte
{
    RGBA32 = 1,
    // Future: BC7, ASTC_4x4, ...
}

/// <summary>Color space the texture samples are meant to be read in.</summary>
public enum TextureColorSpace : byte
{
    Linear = 0,
    SRGB = 1,
}
