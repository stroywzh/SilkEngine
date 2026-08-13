using StbiSharp;

namespace SilkEngine.Core.Assets.Importer;

/// <summary>
/// 基于 StbiSharp（stb_image 原生封装）的解码器
/// <br/>与 StbImageSharpDecoder 互为独立实现，契约测试交叉验证
/// </summary>
public sealed class StbiSharpDecoder : IImageDecoder
{
    /// <inheritdoc/>
    public bool CanDecode(string extension) => extension.ToLowerInvariant() is ".png" or ".jpg";

    /// <inheritdoc/>
    public ImageData Decode(byte[] raw)
    {
        try
        {
            using var image = Stbi.LoadFromMemory(raw, 4);
            if (image is null)
                throw new InvalidOperationException($"StbiSharp 解码失败: {Stbi.FailureReason()}");
            return new ImageData(image.Width, image.Height, image.Data.ToArray());
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"StbiSharp 解码失败: {ex.Message}", ex);
        }
    }
}
