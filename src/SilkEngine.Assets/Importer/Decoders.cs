namespace SilkEngine.Assets.Importer;

/// <summary>解码器注册点：Default 可整体切换，ImporterFactory 按需取用</summary>
public static class Decoders
{
    /// <summary>默认解码器（可切换）</summary>
    public static IImageDecoder Default { get; set; } = new StbImageSharpDecoder();
}
