using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;

namespace SilkEngine;

/// <summary>Runtime texture asset: size and format description.</summary>
public sealed record Texture : IAsset
{
    /// <summary>Creates an unbound descriptor (used by loaders).</summary>
    public Texture() { }

    /// <summary>Creates the descriptor for an asset already registered in the asset table.</summary>
    public Texture(AssetId assetId)
    {
        AssetId = assetId;
    }

    /// <summary>Width in pixels.</summary>
    public int Width { get; init; }

    /// <summary>Height in pixels.</summary>
    public int Height { get; init; }

    /// <summary>Pixel format of the payload.</summary>
    public TextureFormat Format { get; init; }

    /// <summary>Color space the samples are meant to be read in.</summary>
    public TextureColorSpace ColorSpace { get; init; }

    /// <summary>Number of mip levels in the payload.</summary>
    public int MipCount { get; init; }

    /// <summary>Asset identity (<see cref="AssetTable"/> key); used to fetch the pixel bytes.</summary>
    public AssetId AssetId { get; init; }

    /// <inheritdoc />
    public IAsset WithAssetId(AssetId assetId) => this with { AssetId = assetId };
}
