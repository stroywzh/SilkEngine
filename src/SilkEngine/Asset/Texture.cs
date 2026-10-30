using SilkEngine.Core.AssetSystem;

namespace SilkEngine.Asset;

/// <summary>
/// Runtime texture asset: size and format description.
/// </summary>
public sealed record Texture : IAsset
{
    /// <summary>Creates the descriptor for an asset already registered in the asset table.</summary>
    public Texture(AssetId assetId)
    {
        AssetId = assetId;
    }

    /// <summary>Width in pixels.</summary>
    public int Width;

    /// <summary>Height in pixels.</summary>
    public int Height;

    /// <summary>Pixel format of the payload.</summary>
    public TextureFormat Format;

    /// <summary>Color space the samples are meant to be read in.</summary>
    public TextureColorSpace ColorSpace;

    /// <summary>Number of mip levels in the payload.</summary>
    public int MipCount;

    /// <summary>Asset identity (<see cref="AssetTable"/> key); used to fetch the pixel bytes.</summary>
    public AssetId AssetId { get; init; }
}
