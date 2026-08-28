using SilkEngine.Assets;
using SilkEngine.Assets.Importer;
using SilkEngine.Assets.Serialization;
using SilkEngine.Assets.VirtualFileSystem;
using SilkEngine.Core;
using SilkEngine.Math;
using SilkEngine.Render;

namespace SilkEngine.Tests.Core.Assets;

/// <summary>测试序列化器：按声明类型与版本范围实现契约，不承载真实载荷（测试夹具）</summary>
public sealed class TestSerializer(AssetTypeId typeId, int minVersion, int maxVersion) : IAssetSerializer
{
    /// <summary>声明支持的资产类型</summary>
    public AssetTypeId TypeId { get; } = typeId;

    /// <summary>支持的最小 schema 版本</summary>
    public int MinVersion { get; } = minVersion;

    /// <summary>支持的最大 schema 版本</summary>
    public int MaxVersion { get; } = maxVersion;

    /// <summary>判断版本是否在声明范围内</summary>
    public bool SupportsVersion(int schemaVersion) => schemaVersion >= MinVersion && schemaVersion <= MaxVersion;

    /// <summary>生成最小记录（无依赖，数据为占位 JSON）</summary>
    public AssetSerializationRecord Serialize(object asset) => new()
    {
        SchemaVersion = MinVersion,
        TypeId = TypeId,
        Data = "{}"
    };

    /// <summary>原样返回记录（测试夹具不做解码）</summary>
    public object Deserialize(AssetSerializationRecord record, IAssetReferenceResolver resolver) => record;
}

/// <summary>资产序列化测试夹具：构造序列化记录与资产图（测试夹具）</summary>
public static class Fixtures
{
    /// <summary>构造最小序列化记录；type/version/assetId 可覆盖</summary>
    /// <param name="type">资产类型标识（默认 material）</param>
    /// <param name="version">schema 版本（默认 1）</param>
    /// <param name="assetId">资产 ID（默认随机）</param>
    /// <returns>序列化记录</returns>
    public static AssetSerializationRecord SerializationRecord(string? type = null, int version = 1, AssetId? assetId = null)
    {
        return new AssetSerializationRecord
        {
            SchemaVersion = version,
            TypeId = new AssetTypeId(type ?? "material"),
            AssetId = assetId ?? new AssetId(Guid.NewGuid()),
            SourceNodeId = new VirtualNodeId(Guid.NewGuid()),
            Dependencies = [],
            Data = "{}"
        };
    }

    /// <summary>构造带着色器/纹理依赖与三类型默认参数的材质资产（测试夹具）</summary>
    /// <returns>材质资产</returns>
    public static MaterialAsset MaterialAssetWithDependencies()
    {
        return new MaterialAsset(
            new AssetId(Guid.NewGuid()),
            new AssetHandle<ShaderAsset>(new AssetId(Guid.NewGuid())),
            new AssetHandle<TextureAsset>(new AssetId(Guid.NewGuid())),
            new MaterialParameterSnapshot([
                ("Tint", MaterialValue.Vector3(new Vector3(1f, 0f, 0f))),
                ("Opacity", MaterialValue.Float(0.5f)),
                ("World", MaterialValue.Matrix4x4(Matrix4x4.CreateScale(new Vector3(2f, 3f, 4f)))),
            ]),
            revision: 7);
    }

    /// <summary>缺失依赖测试用材质资产 ID（记录存在于 MissingReferenceResolver，依赖记录不存在）</summary>
    public static AssetId MaterialAssetId { get; } = new(Guid.NewGuid());

    /// <summary>循环依赖测试的入口资产 ID（A 依赖 B，B 依赖 A）</summary>
    public static AssetId CyclicAssetId { get; } = new(Guid.NewGuid());

    /// <summary>循环依赖测试的第二个资产 ID</summary>
    public static AssetId CyclicDependencyId { get; } = new(Guid.NewGuid());

    /// <summary>构造材质-着色器-纹理依赖图记录（数据由真实序列化器编码，材质依赖顺序：着色器在前、纹理在后）</summary>
    /// <returns>依赖图记录</returns>
    public static AssetGraphRecords MaterialGraphRecords()
    {
        var shaderId = new AssetId(Guid.NewGuid());
        var textureId = new AssetId(Guid.NewGuid());
        var materialId = new AssetId(Guid.NewGuid());

        var shader = new ShaderAssetSerializer().Serialize(
            new ShaderAsset("lit", "#version 330 core", "void main(){}")) with
        {
            AssetId = shaderId,
        };
        var texture = new TextureAssetSerializer().Serialize(
            new TextureAsset("white", new ImageData(1, 1, [255, 255, 255, 255]))) with
        {
            AssetId = textureId,
        };
        var material = new MaterialAssetSerializer().Serialize(new MaterialAsset(
            materialId,
            new AssetHandle<ShaderAsset>(shaderId),
            new AssetHandle<TextureAsset>(textureId),
            new MaterialParameterSnapshot([("Opacity", MaterialValue.Float(1f))])));

        return new AssetGraphRecords(material, shader, texture);
    }

    /// <summary>构造互相依赖的两个着色器记录（A 依赖 B，B 依赖 A）</summary>
    /// <returns>循环图记录数组</returns>
    public static AssetSerializationRecord[] CyclicGraphRecords()
    {
        var a = Fixtures.SerializationRecord(type: "shader", assetId: CyclicAssetId) with
        {
            Dependencies = [new UntypedAssetHandle(CyclicDependencyId, new AssetTypeId("shader"))],
        };
        var b = Fixtures.SerializationRecord(type: "shader", assetId: CyclicDependencyId) with
        {
            Dependencies = [new UntypedAssetHandle(CyclicAssetId, new AssetTypeId("shader"))],
        };
        return [a, b];
    }

    /// <summary>构造预注册内置序列化器的反序列化服务（测试夹具）</summary>
    /// <param name="resolver">引用解析器</param>
    /// <returns>反序列化服务</returns>
    public static AssetSerializationService SerializationService(IAssetReferenceResolver resolver)
    {
        var registry = new AssetSerializerRegistry();
        registry.Register(new TextureAssetSerializer());
        registry.Register(new ShaderAssetSerializer());
        registry.Register(new MeshAssetSerializer());
        registry.Register(new MaterialAssetSerializer());
        return new AssetSerializationService(registry, resolver);
    }

    /// <summary>构造自足 AssetManager（注入空序列化器注册表，实例间互不影响）并注销其 ctor 自注册（消除 ambient 依赖）</summary>
    /// <param name="files">资产文件服务（默认内存文件系统）</param>
    /// <returns>可独立使用的 AssetManager 实例</returns>
    public static AssetManager AssetManagerWithSerializerRegistry(IAssetFileSystem? files = null)
    {
        var manager = TestAssetPipeline.CreateManager(files);
        Services.Unregister<AssetManager>();
        return manager;
    }

    /// <summary>构造自足 AssetManager 上下文（含线程运行时与管线；供测试排空 FrameCommit 应用缓存）</summary>
    /// <param name="files">资产文件服务（默认内存文件系统）</param>
    /// <returns>管理器上下文（ctor 自注册；调用方负责注销）</returns>
    internal static ManagerContext AssetManagerContext(IAssetFileSystem? files = null)
        => TestAssetPipeline.CreateContext(files);

    /// <summary>按源材质引用构造其资产序列化记录（夹具重建源资产载荷；记录绝不携带实例覆盖）</summary>

    /// <summary>构造带实例覆盖参数的材质实例（覆盖 "Opacity"；源引用指向独立资产 ID）</summary>
    /// <returns>材质运行时实例</returns>
    public static Material MaterialInstanceWithOverride()
    {
        var material = new Material(new MaterialReference(new AssetId(Guid.NewGuid())));
        material.SetFloat("Opacity", 0.9f);
        return material;
    }

    /// <summary>按源材质引用构造其资产序列化记录（夹具重建源资产载荷；记录绝不携带实例覆盖）</summary>
    /// <param name="source">源材质资产引用</param>
    /// <returns>材质资产记录</returns>
    public static AssetSerializationRecord SerializeMaterialAsset(MaterialReference source)
    {
        var asset = new MaterialAsset(
            source.AssetId,
            new AssetHandle<ShaderAsset>(new AssetId(Guid.NewGuid())),
            new AssetHandle<TextureAsset>(new AssetId(Guid.NewGuid())),
            new MaterialParameterSnapshot([("Opacity", MaterialValue.Float(1f))]),
            revision: 1);
        return new MaterialAssetSerializer().Serialize(asset);
    }
}

/// <summary>材质依赖图记录容器（测试夹具）</summary>
/// <param name="Material">材质记录（依赖着色器与纹理）</param>
/// <param name="Shader">着色器记录（无依赖）</param>
/// <param name="Texture">纹理记录（无依赖）</param>
public sealed record AssetGraphRecords(
    AssetSerializationRecord Material,
    AssetSerializationRecord Shader,
    AssetSerializationRecord Texture);

/// <summary>记录型引用解析器：按记录目录提供查询，记录每次句柄解析调用（测试夹具）</summary>
public sealed class RecordingReferenceResolver : IAssetReferenceResolver
{
    private readonly Dictionary<AssetId, AssetSerializationRecord> _records;

    /// <summary>按解析顺序记录的全部依赖 ID</summary>
    public List<AssetId> ResolvedIds { get; } = [];

    /// <summary>以记录集合创建解析器</summary>
    /// <param name="records">记录集合</param>
    public RecordingReferenceResolver(params AssetSerializationRecord[] records)
    {
        _records = records.ToDictionary(r => r.AssetId);
    }

    /// <summary>按资产 ID 查询记录；未命中返回 null</summary>
    public AssetSerializationRecord? TryGetRecord(AssetId assetId)
        => _records.TryGetValue(assetId, out var record) ? record : null;

    /// <summary>记录强类型句柄解析（返回 null，测试不消费解析结果）</summary>
    public T Resolve<T>(AssetHandle<T> handle)
        where T : class
    {
        ResolvedIds.Add(handle.Id);
        return null!;
    }

    /// <summary>记录非泛型句柄解析（返回 null，测试不消费解析结果）</summary>
    public object Resolve(UntypedAssetHandle handle)
    {
        ResolvedIds.Add(handle.Id);
        return null!;
    }
}

/// <summary>缺失依赖解析器：提供材质记录但缺失其着色器依赖（测试夹具）</summary>
public sealed class MissingReferenceResolver : IAssetReferenceResolver
{
    private readonly AssetSerializationRecord _material = new()
    {
        SchemaVersion = 1,
        TypeId = new AssetTypeId("material"),
        AssetId = Fixtures.MaterialAssetId,
        Dependencies =
        [
            new UntypedAssetHandle(new AssetId(Guid.NewGuid()), new AssetTypeId("shader")),
        ],
        Data = "{}",
    };

    /// <summary>仅返回材质记录；依赖记录一律未命中</summary>
    public AssetSerializationRecord? TryGetRecord(AssetId assetId)
        => assetId == _material.AssetId ? _material : null;

    /// <summary>抛 <see cref="KeyNotFoundException"/>（依赖不存在）</summary>
    public T Resolve<T>(AssetHandle<T> handle)
        where T : class
        => throw new KeyNotFoundException($"依赖 {handle.Id} 不存在");

    /// <summary>抛 <see cref="KeyNotFoundException"/>（依赖不存在）</summary>
    public object Resolve(UntypedAssetHandle handle)
        => throw new KeyNotFoundException($"依赖 {handle.Id} 不存在");
}

/// <summary>空操作引用解析器：不解析任何依赖（测试夹具）</summary>
public sealed class NoopReferenceResolver : IAssetReferenceResolver
{
    /// <summary>始终返回 null（不提供记录）</summary>
    public AssetSerializationRecord? TryGetRecord(AssetId assetId) => null;

    /// <summary>返回 null（不解析依赖）</summary>
    public T Resolve<T>(AssetHandle<T> handle)
        where T : class => null!;

    /// <summary>返回 null（不解析依赖）</summary>
    public object Resolve(UntypedAssetHandle handle) => null!;
}
