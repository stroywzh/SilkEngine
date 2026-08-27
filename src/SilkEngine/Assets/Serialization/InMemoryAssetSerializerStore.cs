namespace SilkEngine.Assets.Serialization;

/// <summary>
/// 内存序列化记录存储：按 <see cref="AssetId"/> 保存记录，重复保存同 ID 覆盖旧记录；
/// 记录本身不可变且依赖列表在写入时复制，保存引用即等价于复制内容。
/// </summary>
public sealed class InMemoryAssetSerializerStore : IAssetSerializerStore
{
    private readonly Dictionary<AssetId, AssetSerializationRecord> _records = [];

    /// <summary>保存记录；同 ID 重复保存覆盖旧记录</summary>
    /// <param name="record">待保存记录；null 抛 <see cref="ArgumentNullException"/></param>
    /// <returns>同步完成的 Task</returns>
    public Task SaveAsync(AssetSerializationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _records[record.AssetId] = record;
        return Task.CompletedTask;
    }

    /// <summary>按资产 ID 读取记录</summary>
    /// <param name="assetId">资产 ID</param>
    /// <returns>序列化记录；未命中返回 null</returns>
    public Task<AssetSerializationRecord?> LoadAsync(AssetId assetId)
        => Task.FromResult(_records.TryGetValue(assetId, out var record) ? record : null);
}
