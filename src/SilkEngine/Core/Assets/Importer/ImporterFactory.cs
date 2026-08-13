namespace SilkEngine.Core.Assets.Importer;

/// <summary>按扩展名创建导入器</summary>
public static class ImporterFactory
{
    /// <summary>创建扩展名对应的导入器；不支持的扩展名抛异常（扩展名大小写不敏感）</summary>
    public static IAssetImporter Create(string extension, ImportSettings? settings = null) =>
        extension.ToLowerInvariant() switch
        {
            ".png" or ".jpg" => new TextureImporter(Decoders.Default, settings),
            _ => throw new NotSupportedException($"No importer for extension '{extension}'"),
        };
}
