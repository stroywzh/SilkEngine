using SilkEngine.Asset;

namespace SilkEngine;

/// <summary>Runtime texture asset: size and format description.</summary>
public sealed class Texture : AssetBase
{
    /// <inheritdoc />
    public override string AssetTypeName => TextureAssetName;

    /// <summary>Creates an unbound descriptor (used by loaders).</summary>
    internal Texture()
        : base() { }

    /// <summary>Creates the descriptor bound to its store-issued handle.</summary>
    internal Texture(AssetHandle handle)
        : base(handle) { }

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
