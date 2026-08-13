namespace SilkEngine.Core.Assets.Importer;

/// <summary>图像解码器：raw 字节 → 纯 CPU ImageData</summary>
public interface IImageDecoder
{
    /// <summary>解码图像；失败时抛出异常（统一包装为 InvalidOperationException）</summary>
    ImageData Decode(byte[] raw);

    /// <summary>是否支持该扩展名（大小写不敏感，含点，如 ".png"）</summary>
    bool CanDecode(string extension);
}
