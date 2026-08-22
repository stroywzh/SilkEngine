using StbImageSharp;

namespace SilkEngine.Assets.Importer;

/// <summary>基于 StbImageSharp（纯托管 stb_image 移植）的解码器</summary>
public sealed class StbImageSharpDecoder : IImageDecoder
{
    /// <inheritdoc/>
    public bool CanDecode(string extension) => extension.ToLowerInvariant() is ".png" or ".jpg";

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">解码失败（包装异常信息）</exception>
    public ImageData Decode(byte[] raw)
    {
        try
        {
            var result = ImageResult.FromMemory(raw, ColorComponents.RedGreenBlueAlpha);
            return new ImageData(result.Width, result.Height, result.Data);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"StbImageSharp 解码失败: {ex.Message}", ex);
        }
    }
}
