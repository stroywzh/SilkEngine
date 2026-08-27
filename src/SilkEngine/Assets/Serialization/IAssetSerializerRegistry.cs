namespace SilkEngine.Assets.Serialization;

/// <summary>序列化器注册表：按资产类型查找序列化器，并校验 schema 版本支持范围</summary>
public interface IAssetSerializerRegistry
{
    /// <summary>注册序列化器；类型重复抛 <see cref="InvalidOperationException"/>，null 或空类型抛 <see cref="ArgumentException"/></summary>
    /// <param name="serializer">待注册序列化器</param>
    void Register(IAssetSerializer serializer);

    /// <summary>按类型与 schema 版本解析序列化器；未知类型或版本不支持抛 <see cref="NotSupportedException"/></summary>
    /// <param name="typeId">资产类型标识</param>
    /// <param name="schemaVersion">记录 schema 版本</param>
    /// <returns>匹配的序列化器</returns>
    IAssetSerializer Resolve(AssetTypeId typeId, int schemaVersion);
}
