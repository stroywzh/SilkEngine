namespace SilkEngine.Assets;

/// <summary>纯 CPU 图像数据（RGBA8，无 GL 依赖）</summary>
public sealed class ImageData : IAssetDataRaw
{
    /// <summary>创建图像数据；像素长度不足 width*height*4 抛 ArgumentException。</summary>
    /// <param name="width">像素宽</param>
    /// <param name="height">像素高</param>
    /// <param name="raw">RGBA 像素数据，长度 = width*height*4</param>
    /// <exception cref="ArgumentException">pixels 长度不足 width*height*4</exception>
    public ImageData(int width, int height, byte[] raw)
    {
        if (raw.Length < width * height * 4)
            throw new ArgumentException(
                $"像素长度 {raw.Length} 不足：{width}x{height}x4 需要 {width * height * 4}",
                nameof(raw)
            );
        Width = width;
        Height = height;
        RawBytes = raw;
    }

    /// <summary>像素宽</summary>
    public int Width { get; }

    /// <summary>像素高</summary>
    public int Height { get; }

    /// <summary>RGBA 像素数据（R,G,B,A 顺序，行序自上而下）</summary>
    public byte[] RawBytes { get; init; }
}
