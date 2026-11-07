using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Asset.Payload;

/// <summary>纹理 CPU 载荷：已解码的 RGBA32 像素。</summary>
internal sealed class TexturePayload : IAssetPayload
{
    public required ReadOnlyMemory<byte> Rgba32 { get; init; }

    /// <summary>释放解码后的像素拷贝；释放后不得再读取本载荷。</summary>
    public void Dispose() { }
}
