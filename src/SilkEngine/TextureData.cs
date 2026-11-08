using SilkEngine.Asset;

namespace SilkEngine;

/// <summary>Texture content: pixel dimensions, format, color space and mip count.</summary>
public sealed record TextureData
{
    /// <summary>Describes texture content; dimensions and the mip count must be positive.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/>, <paramref name="height"/> or <paramref name="mipCount"/> is not positive.</exception>
    public TextureData(
        int width,
        int height,
        TextureFormat format = TextureFormat.RGBA32,
        TextureColorSpace colorSpace = TextureColorSpace.SRGB,
        int mipCount = 1
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(mipCount);

        Width = width;
        Height = height;
        Format = format;
        ColorSpace = colorSpace;
        MipCount = mipCount;
    }

    /// <summary>Width in pixels.</summary>
    public int Width { get; }

    /// <summary>Height in pixels.</summary>
    public int Height { get; }

    /// <summary>Pixel format of the payload.</summary>
    public TextureFormat Format { get; }

    /// <summary>Color space the samples are meant to be read in.</summary>
    public TextureColorSpace ColorSpace { get; }

    /// <summary>Number of mip levels in the payload.</summary>
    public int MipCount { get; }
}
