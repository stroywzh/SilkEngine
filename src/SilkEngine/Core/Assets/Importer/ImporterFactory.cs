namespace SilkEngine.Core.Assets.Importer;

/// <summary>按扩展名创建导入器：内置注册 .png/.jpg，新导入器经 <see cref="Register"/> 接入（开闭原则）</summary>
public static class ImporterFactory
{
    private static readonly Dictionary<string, Func<ImportSettings?, IAssetImporter>> _registry = new(StringComparer.OrdinalIgnoreCase);

    static ImporterFactory()
    {
        _registry[".png"] = settings => new TextureImporter(Decoders.Default, settings);
        _registry[".jpg"] = settings => new TextureImporter(Decoders.Default, settings);
    }

    /// <summary>注册扩展名对应的导入器工厂（扩展名大小写不敏感，含点）。</summary>
    /// <param name="extension">文件扩展名（如 ".png"）</param>
    /// <param name="factory">导入器工厂（Create 时以 settings 调用）</param>
    /// <exception cref="InvalidOperationException">扩展名已注册</exception>
    public static void Register(string extension, Func<ImportSettings?, IAssetImporter> factory)
    {
        if (!_registry.TryAdd(extension, factory))
        {
            throw new InvalidOperationException($"Importer for extension '{extension}' is already registered");
        }
    }

    /// <summary>创建扩展名对应的导入器（扩展名大小写不敏感，含点）。</summary>
    /// <param name="extension">文件扩展名（如 ".png"）</param>
    /// <param name="settings">导入设置（透传给导入器工厂）</param>
    /// <returns>对应扩展名的导入器实例</returns>
    /// <exception cref="NotSupportedException">扩展名无对应导入器</exception>
    public static IAssetImporter Create(string extension, ImportSettings? settings = null)
    {
        if (_registry.TryGetValue(extension, out var factory))
        {
            return factory(settings);
        }
        throw new NotSupportedException($"No importer for extension '{extension}'");
    }
}
