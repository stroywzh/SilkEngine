namespace SilkEngine.Core.Assets.Importer;

/// <summary>纹理导入器：委托 IImageDecoder 解码</summary>
public sealed class TextureImporter : IAssetImporter
{
    private readonly IImageDecoder _decoder;

    /// <summary>支持的扩展名</summary>
    public string[] Extensions { get; } = [".png", ".jpg"];

    /// <summary>创建纹理导入器</summary>
    /// <param name="decoder">解码器（构造注入）</param>
    /// <param name="settings">导入设置（当前未使用，保留为扩展点）</param>
    public TextureImporter(IImageDecoder decoder, ImportSettings? settings = null)
    {
        _decoder = decoder;
    }

    /// <summary>导入：解码为 Texture2D</summary>
    public IAsset Import(byte[] raw, ImportSettings? settings = null)
    {
        var data = _decoder.Decode(raw);
        return new Texture2D { Name = "Texture", ImageData = data };
    }
}
