namespace SilkEngine.Assets.Importer;

/// <summary>按扩展名与资产类型解析导入器的实例注册表：默认注册 .png/.jpg → 纹理、.hlsl → 着色器、.obj → 网格、.asset → 材质；新导入器经 <see cref="Register"/> 接入</summary>
public sealed class AssetImporterRegistry
{
    /// <summary>纹理资产类型标识（默认注册与兼容门面共用）</summary>
    public static readonly AssetTypeId TextureAssetTypeId = new("texture");

    /// <summary>着色器资产类型标识</summary>
    public static readonly AssetTypeId ShaderAssetTypeId = new("shader");

    /// <summary>网格资产类型标识</summary>
    public static readonly AssetTypeId MeshAssetTypeId = new("mesh");

    /// <summary>材质资产类型标识</summary>
    public static readonly AssetTypeId MaterialAssetTypeId = new("material");

    private readonly Dictionary<string, Entry> _importers = new(StringComparer.OrdinalIgnoreCase);

    private sealed record Entry(AssetTypeId TypeId, Func<ImportSettings?, IAssetImporter> Factory);

    /// <summary>创建注册表并注册内置默认导入器（纹理/着色器/网格/材质）</summary>
    public AssetImporterRegistry()
        : this(registerDefaults: true)
    {
    }

    /// <summary>创建注册表；registerDefaults 为 false 时为空注册表（测试夹具用）</summary>
    /// <param name="registerDefaults">是否注册内置默认导入器</param>
    internal AssetImporterRegistry(bool registerDefaults)
    {
        if (!registerDefaults)
            return;
        Register(TextureAssetTypeId, ".png", settings => new TextureImporter(Decoders.Default, settings));
        Register(TextureAssetTypeId, ".jpg", settings => new TextureImporter(Decoders.Default, settings));
        Register(ShaderAssetTypeId, ".hlsl", _ => new ShaderImporter());
        Register(MeshAssetTypeId, ".obj", _ => new ObjMeshImporter());
        Register(MaterialAssetTypeId, ".asset", _ => new MaterialImporter());
    }

    /// <summary>注册扩展名对应的导入器工厂（扩展名大小写不敏感、含点处理）。</summary>
    /// <param name="assetTypeId">该扩展名所属的资产类型</param>
    /// <param name="extension">文件扩展名（如 ".png" 或 "png"）</param>
    /// <param name="factory">导入器工厂（Resolve 时以 settings 调用）</param>
    /// <exception cref="InvalidOperationException">扩展名已注册</exception>
    public void Register(AssetTypeId assetTypeId, string extension, Func<ImportSettings?, IAssetImporter> factory)
    {
        var key = NormalizeExtension(extension);
        if (!_importers.TryAdd(key, new Entry(assetTypeId, factory)))
        {
            throw new InvalidOperationException($"Importer for extension '{extension}' is already registered");
        }
    }

    /// <summary>解析扩展名与类型对应的导入器（扩展名大小写不敏感、含点处理）。</summary>
    /// <param name="assetTypeId">期望的资产类型</param>
    /// <param name="extension">文件扩展名（如 ".png" 或 "png"）</param>
    /// <param name="settings">导入设置（透传给导入器工厂）</param>
    /// <returns>对应扩展名的导入器实例</returns>
    /// <exception cref="NotSupportedException">扩展名无对应导入器，或扩展名注册的资产类型与请求不一致</exception>
    public IAssetImporter Resolve(AssetTypeId assetTypeId, string extension, ImportSettings? settings = null)
    {
        if (!_importers.TryGetValue(NormalizeExtension(extension), out var entry))
        {
            throw new NotSupportedException($"No importer for extension '{extension}'");
        }
        if (entry.TypeId != assetTypeId)
        {
            throw new NotSupportedException(
                $"Importer for extension '{extension}' is registered for asset type '{entry.TypeId.Value}', not '{assetTypeId.Value}'");
        }
        return entry.Factory(settings);
    }

    /// <summary>查询扩展名对应的资产类型（解析前置：AssetManager 据此登记目录记录；扩展名大小写不敏感、含点处理）。</summary>
    /// <param name="extension">文件扩展名（如 ".png" 或 "png"）</param>
    /// <param name="assetTypeId">命中的资产类型（未命中为 default）</param>
    /// <returns>扩展名已注册时为 true</returns>
    public bool TryGetAssetType(string extension, out AssetTypeId assetTypeId)
    {
        if (_importers.TryGetValue(NormalizeExtension(extension), out var entry))
        {
            assetTypeId = entry.TypeId;
            return true;
        }
        assetTypeId = default;
        return false;
    }

    private static string NormalizeExtension(string extension) => "." + extension.Trim().TrimStart('.');
}
