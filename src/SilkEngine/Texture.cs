using SilkEngine.Asset;

namespace SilkEngine;

/// <summary>Runtime texture asset: its content description plus the store identity of the loaded record.</summary>
public sealed class Texture : AssetBase
{
    /// <inheritdoc />
    public override string AssetTypeName => TextureAssetName;

    /// <summary>Creates a texture bound to its store-issued handle and carrying the given content.</summary>
    internal Texture(AssetHandle handle, TextureData data)
        : base(handle)
    {
        Data = data;
    }

    /// <summary>Texture content uploaded by the render side.</summary>
    public TextureData Data { get; }

    /// <summary>Width in pixels.</summary>
    public int Width => Data.Width;

    /// <summary>Height in pixels.</summary>
    public int Height => Data.Height;

    /// <summary>Pixel format of the payload.</summary>
    public TextureFormat Format => Data.Format;

    /// <summary>Color space the samples are meant to be read in.</summary>
    public TextureColorSpace ColorSpace => Data.ColorSpace;

    /// <summary>Number of mip levels in the payload.</summary>
    public int MipCount => Data.MipCount;
}
