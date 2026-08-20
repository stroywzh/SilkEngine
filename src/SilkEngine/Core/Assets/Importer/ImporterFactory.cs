namespace SilkEngine.Core.Assets.Importer;

/// <summary>按扩展名创建导入器</summary>
public static class ImporterFactory
{
    /// <summary>创建扩展名对应的导入器（扩展名大小写不敏感，含点）。</summary>
    /// <param name="extension">文件扩展名（如 ".png"）</param>
    /// <param name="settings">导入设置（透传给导入器）</param>
    /// <returns>对应扩展名的导入器实例</returns>
    /// <exception cref="NotSupportedException">扩展名无对应导入器</exception>
    public static IAssetImporter Create(string extension, ImportSettings? settings = null) =>
        extension.ToLowerInvariant() switch
        {
            ".png" or ".jpg" => new TextureImporter(Decoders.Default, settings),
            _ => throw new NotSupportedException($"No importer for extension '{extension}'"),
        };
}
