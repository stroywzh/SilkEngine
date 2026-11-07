using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;

namespace SilkEngine;

/// <summary>Runtime texture asset: size and format description.</summary>
public sealed record Texture : AssetBase
{
    /// <summary>Creates an unbound descriptor (used by loaders).</summary>
    internal Texture()
        : base() { }

    /// <summary>Creates the descriptor for an asset already registered in the asset store.</summary>
    public Texture(AssetId assetId)
        : base(assetId) { }

    /// <summary>Width in pixels.</summary>
    public required int Width { get; init; }

    /// <summary>Height in pixels.</summary>
    public required int Height { get; init; }

    /// <summary>Pixel format of the payload.</summary>
    public TextureFormat Format { get; init; } = TextureFormat.RGBA32;

    /// <summary>Color space the samples are meant to be read in.</summary>
    public TextureColorSpace ColorSpace { get; init; } = TextureColorSpace.SRGB;

    /// <summary>Number of mip levels in the payload.</summary>
    public int MipCount { get; init; } = 4;
}
