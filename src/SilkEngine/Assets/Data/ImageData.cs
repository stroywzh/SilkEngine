namespace SilkEngine.Assets;

/// <summary>纯 CPU 图像数据（RGBA8，无 GL 依赖）；像素数组在构造时复制，发布后不可变</summary>
public sealed class ImageData
{
    /// <summary>创建图像数据；像素长度必须等于 width*height*4（构造时复制，调用方后续修改不影响实例）。</summary>
    /// <param name="width">像素宽</param>
    /// <param name="height">像素高</param>
    /// <param name="raw">RGBA 像素数据，长度 = width*height*4</param>
    /// <exception cref="ArgumentNullException">raw 为 null</exception>
    /// <exception cref="ArgumentException">width/height 为负或像素长度不等于 width*height*4</exception>
    public ImageData(int width, int height, byte[] raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        if (width < 0 || height < 0 || raw.Length != checked(width * height * 4))
            throw new ArgumentException("RGBA8 data length must equal width * height * 4.", nameof(raw));
        Width = width;
        Height = height;
        RawBytes = raw.ToArray();
    }

    /// <summary>像素宽</summary>
    public int Width { get; }

    /// <summary>像素高</summary>
    public int Height { get; }

    /// <summary>RGBA 像素数据（R,G,B,A 顺序，行序自上而下）；构造时的私有副本</summary>
    public byte[] RawBytes { get; }
}
