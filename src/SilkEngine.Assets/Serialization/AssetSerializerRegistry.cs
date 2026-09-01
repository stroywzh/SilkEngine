namespace SilkEngine.Assets.Serialization;

/// <summary>显式序列化器注册表：实例级字典按 <see cref="AssetTypeId"/> 查找，实例之间不共享状态</summary>
public sealed class AssetSerializerRegistry : IAssetSerializerRegistry
{
    private readonly Dictionary<AssetTypeId, IAssetSerializer> _serializers = [];

    /// <summary>注册序列化器；同类型重复注册抛 <see cref="InvalidOperationException"/></summary>
    /// <param name="serializer">待注册序列化器；null 抛 <see cref="ArgumentNullException"/>，空类型抛 <see cref="ArgumentException"/></param>
    /// <exception cref="InvalidOperationException">类型已注册</exception>
    public void Register(IAssetSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        if (string.IsNullOrEmpty(serializer.TypeId.Value))
            throw new ArgumentException("序列化器类型标识不能为空", nameof(serializer));

        if (!_serializers.TryAdd(serializer.TypeId, serializer))
            throw new InvalidOperationException($"资产类型 '{serializer.TypeId.Value}' 已注册序列化器");
    }

    /// <summary>按类型与 schema 版本解析序列化器；未知类型或版本不支持抛 <see cref="NotSupportedException"/></summary>
    /// <param name="typeId">资产类型标识</param>
    /// <param name="schemaVersion">记录 schema 版本</param>
    /// <returns>匹配的序列化器</returns>
    public IAssetSerializer Resolve(AssetTypeId typeId, int schemaVersion)
    {
        if (!_serializers.TryGetValue(typeId, out var serializer))
            throw new NotSupportedException($"资产类型 '{typeId.Value}' 未注册序列化器");

        if (!serializer.SupportsVersion(schemaVersion))
            throw new NotSupportedException(
                $"资产类型 '{typeId.Value}' 不支持 schema 版本 {schemaVersion}（支持范围 {serializer.MinVersion}~{serializer.MaxVersion}）");

        return serializer;
    }
}
