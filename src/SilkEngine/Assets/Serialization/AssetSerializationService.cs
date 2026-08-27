using System.IO;

namespace SilkEngine.Assets.Serialization;

/// <summary>
/// 资产反序列化服务：读取序列化记录 → 解析序列化器与 schema → 以 visited/active 集合深度优先遍历依赖
/// → 全部依赖成功后反序列化并写入已发布字典（原子发布：任一失败不留下半成品）。
/// 幂等：已发布资产重复反序列化直接返回既有实例，不重复解析依赖。
/// </summary>
public sealed class AssetSerializationService
{
    private readonly IAssetSerializerRegistry _registry;
    private readonly IAssetReferenceResolver _resolver;
    private readonly Dictionary<AssetId, object> _published = [];

    /// <summary>创建反序列化服务</summary>
    /// <param name="registry">序列化器注册表</param>
    /// <param name="resolver">引用解析器（兼记录目录）</param>
    public AssetSerializationService(IAssetSerializerRegistry registry, IAssetReferenceResolver resolver)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    /// <summary>
    /// 按资产 ID 反序列化资产（含全部依赖并原子发布）。
    /// 失败语义：记录缺失抛 <see cref="KeyNotFoundException"/>；循环依赖或数据损坏抛 <see cref="InvalidDataException"/>；
    /// 未知类型或版本不支持抛 <see cref="NotSupportedException"/>。错误消息携带资产 ID、类型、源节点与依赖 ID。
    /// </summary>
    /// <param name="assetId">资产 ID</param>
    /// <returns>反序列化结果（IsSuccess 恒为 true；失败以异常抛出）</returns>
    public AssetDeserializationResult Deserialize(AssetId assetId)
    {
        var asset = DeserializeCore(assetId, []);
        return new AssetDeserializationResult(IsSuccess: true, asset, assetId);
    }

    /// <summary>判断资产是否已成功反序列化并发布</summary>
    /// <param name="assetId">资产 ID</param>
    /// <returns>已发布返回 true</returns>
    public bool Contains(AssetId assetId) => _published.ContainsKey(assetId);

    private object DeserializeCore(AssetId assetId, HashSet<AssetId> active)
    {
        if (_published.TryGetValue(assetId, out var existing))
            return existing;

        var record = _resolver.TryGetRecord(assetId)
            ?? throw new KeyNotFoundException($"资产 {assetId.Value} 未找到序列化记录");

        if (!active.Add(assetId))
            throw new InvalidDataException(
                $"检测到资产依赖循环：资产 {record.AssetId.Value}（类型 {record.TypeId.Value}，源节点 {record.SourceNodeId?.Value}）");

        var serializer = _registry.Resolve(record.TypeId, record.SchemaVersion);

        foreach (var dependency in record.Dependencies)
        {
            try
            {
                DeserializeCore(dependency.Id, active);
            }
            catch (KeyNotFoundException)
            {
                throw new KeyNotFoundException(
                    $"资产 {record.AssetId.Value}（类型 {record.TypeId.Value}，源节点 {record.SourceNodeId?.Value}）的依赖 {dependency.Id.Value}（类型 {dependency.TypeId.Value}）未找到序列化记录");
            }

            _resolver.Resolve(dependency);
        }

        var asset = serializer.Deserialize(record, _resolver);
        _published[assetId] = asset;
        active.Remove(assetId);
        return asset;
    }
}

/// <summary>反序列化结果：IsSuccess 恒为 true（失败路径以异常抛出）</summary>
/// <param name="IsSuccess">是否成功</param>
/// <param name="Asset">反序列化出的资产实例</param>
/// <param name="AssetId">资产 ID</param>
public readonly record struct AssetDeserializationResult(bool IsSuccess, object Asset, AssetId AssetId);
