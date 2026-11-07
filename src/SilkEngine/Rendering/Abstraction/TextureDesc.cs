using SilkEngine.Asset;

namespace SilkEngine.Rendering.Abstraction;

/// <summary>Texture upload description: pixel size, format, color space, mip count and the CPU pixel data.</summary>
public readonly record struct TextureDesc(
    int Width,
    int Height,
    TextureFormat Format,
    TextureColorSpace ColorSpace,
    int MipCount,
    ReadOnlyMemory<byte> Data
);
