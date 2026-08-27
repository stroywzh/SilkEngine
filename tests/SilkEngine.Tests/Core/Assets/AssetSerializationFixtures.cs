using SilkEngine.Assets;
using SilkEngine.Assets.Serialization;

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
