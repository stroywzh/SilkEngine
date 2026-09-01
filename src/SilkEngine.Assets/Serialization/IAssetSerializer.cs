namespace SilkEngine.Assets.Serialization;

/// <summary>
/// 资产序列化器契约：声明支持的资产类型与 schema 版本范围，负责资产与序列化记录的双向转换。
/// 实现必须使用显式 DTO/编码字段，禁止通过反射推断字段；不得接触 GPU 对象、AssetManager 或运行时 lease。
/// </summary>
public interface IAssetSerializer
{
    /// <summary>支持的资产类型标识</summary>
    AssetTypeId TypeId { get; }

    /// <summary>支持的最小 schema 版本</summary>
    int MinVersion { get; }

    /// <summary>支持的最大 schema 版本</summary>
    int MaxVersion { get; }

    /// <summary>判断是否支持指定 schema 版本</summary>
    /// <param name="schemaVersion">待判定版本</param>
    /// <returns>版本落在 [MinVersion, MaxVersion] 范围内时为 true</returns>
    bool SupportsVersion(int schemaVersion);

    /// <summary>
    /// 将资产序列化为记录；载荷自身携带身份时（如 MaterialAsset.Id）填入记录 AssetId，
    /// 纯载荷类型（TextureAsset 等）留默认值，由调用方以 with 表达式补齐。
    /// </summary>
    /// <param name="asset">待序列化资产；类型不匹配抛 <see cref="ArgumentException"/></param>
    /// <returns>序列化记录</returns>
    AssetSerializationRecord Serialize(object asset);

    /// <summary>将记录反序列化为资产；依赖经 resolver 解析</summary>
    /// <param name="record">待反序列化记录；类型或版本不匹配抛 <see cref="NotSupportedException"/>，数据损坏抛 <see cref="System.IO.InvalidDataException"/></param>
    /// <param name="resolver">依赖解析器（依赖已完成反序列化）</param>
    /// <returns>资产实例</returns>
    object Deserialize(AssetSerializationRecord record, IAssetReferenceResolver resolver);
}
