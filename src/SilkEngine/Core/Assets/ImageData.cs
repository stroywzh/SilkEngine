namespace SilkEngine.Core.Assets;

/// <summary>纯 CPU 图像数据（RGBA8，无 GL 依赖）</summary>
/// <param name="width">像素宽</param>
/// <param name="height">像素高</param>
/// <param name="pixels">RGBA 像素数据，长度 = width*height*4</param>
public sealed class ImageData(int width, int height, byte[] pixels)
{
    /// <summary>像素宽</summary>
    public int Width { get; } = width;

    /// <summary>像素高</summary>
    public int Height { get; } = height;

    /// <summary>RGBA 像素数据（R,G,B,A 顺序，行序自上而下）</summary>
    public byte[] Pixels { get; } = pixels;
}
