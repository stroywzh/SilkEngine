namespace SilkEngine.Assets.Importer;

/// <summary>
/// 兼容门面：委托默认 <see cref="AssetImporterRegistry"/> 实例，保留静态入口（内置 .png/.jpg 注册已迁移至注册表默认注册）
/// </summary>
public static class ImporterFactory
{
    private static readonly AssetImporterRegistry Default = new();

    /// <summary>注册扩展名对应的导入器工厂（扩展名大小写不敏感，含点）。</summary>
    /// <param name="extension">文件扩展名（如 ".png"）</param>
    /// <param name="factory">导入器工厂（Create 时以 settings 调用）</param>
    /// <exception cref="InvalidOperationException">扩展名已注册</exception>
    public static void Register(string extension, Func<ImportSettings?, IAssetImporter> factory)
        => Default.Register(AssetImporterRegistry.TextureAssetTypeId, extension, factory);

    /// <summary>创建扩展名对应的导入器（扩展名大小写不敏感，含点）。</summary>
    /// <param name="extension">文件扩展名（如 ".png"）</param>
    /// <param name="settings">导入设置（透传给导入器工厂）</param>
    /// <returns>对应扩展名的导入器实例</returns>
    /// <exception cref="NotSupportedException">扩展名无对应导入器</exception>
    public static IAssetImporter Create(string extension, ImportSettings? settings = null)
        => Default.Resolve(AssetImporterRegistry.TextureAssetTypeId, extension, settings);
}
