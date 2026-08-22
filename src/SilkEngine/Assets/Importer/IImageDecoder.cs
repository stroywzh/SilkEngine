namespace SilkEngine.Assets.Importer;

/// <summary>图像解码器：raw 字节 → 纯 CPU ImageData</summary>
public interface IImageDecoder
{
    /// <summary>解码图像；失败时抛出异常（统一包装为 InvalidOperationException）。</summary>
    /// <param name="raw">图像原始字节</param>
    /// <returns>解码完成的纯 CPU 图像数据</returns>
    ImageData Decode(byte[] raw);

    /// <summary>是否支持该扩展名（大小写不敏感，含点，如 ".png"）。</summary>
    /// <param name="extension">扩展名</param>
    /// <returns>支持为 true</returns>
    bool CanDecode(string extension);
}
