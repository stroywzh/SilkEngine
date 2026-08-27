using SilkEngine.Assets;
using SilkEngine.Assets.Serialization;
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
