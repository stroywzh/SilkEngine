namespace SilkEngine.Assets.Serialization;

/// <summary>
/// 内存序列化记录存储：按 <see cref="AssetId"/> 保存记录，重复保存同 ID 覆盖旧记录；
/// 记录本身不可变且依赖列表在写入时复制，保存引用即等价于复制内容。
/// 加载支持期望三元组（BuildKey/SourceFingerprint/ImporterRevision）：任一提供且与记录不一致即 miss。
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

    /// <summary>
    /// 按资产 ID 读取记录；期望三元组任一项与记录不一致即视为未命中返回 null
    /// （期望值为 null 的项不做约束）。
    /// </summary>
    /// <param name="assetId">资产 ID</param>
    /// <param name="buildKey">期望构建键（null 不约束）</param>
    /// <param name="sourceFingerprint">期望源内容指纹（null 不约束）</param>
    /// <param name="importerRevision">期望导入器修订号（null 不约束）</param>
    /// <returns>序列化记录；未命中返回 null</returns>
    public Task<AssetSerializationRecord?> LoadAsync(
        AssetId assetId,
        string? buildKey = null,
        string? sourceFingerprint = null,
        ulong? importerRevision = null)
    {
        if (!_records.TryGetValue(assetId, out var record))
            return Task.FromResult<AssetSerializationRecord?>(null);
        if (buildKey is not null && !string.Equals(buildKey, record.BuildKey, StringComparison.Ordinal))
            return Task.FromResult<AssetSerializationRecord?>(null);
        if (sourceFingerprint is not null && !string.Equals(sourceFingerprint, record.SourceFingerprint, StringComparison.Ordinal))
            return Task.FromResult<AssetSerializationRecord?>(null);
        if (importerRevision is not null && importerRevision != record.ImporterRevision)
            return Task.FromResult<AssetSerializationRecord?>(null);
        return Task.FromResult<AssetSerializationRecord?>(record);
    }
}