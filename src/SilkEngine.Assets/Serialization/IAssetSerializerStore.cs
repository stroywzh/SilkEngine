namespace SilkEngine.Assets.Serialization;

/// <summary>
/// 序列化记录存储：按资产 ID 保存/读取序列化记录。
/// 实现负责数据持久化，不得改变记录内容；未命中约定由实现文档说明（返回 null 或抛 <see cref="System.Collections.Generic.KeyNotFoundException"/>）。
/// 记录携带 <see cref="AssetSerializationRecord.BuildKey"/> / <see cref="AssetSerializationRecord.SourceFingerprint"/> /
/// <see cref="AssetSerializationRecord.ImporterRevision"/> 语义：加载时可提供期望三元组，命中必须与记录同时匹配（任一不匹配即 miss）。
/// </summary>
public interface IAssetSerializerStore
{
    /// <summary>保存序列化记录（同步完成）</summary>
    /// <param name="record">待保存记录</param>
    Task SaveAsync(AssetSerializationRecord record);

    /// <summary>
    /// 按资产 ID 读取序列化记录；提供期望三元组时，记录的 BuildKey/SourceFingerprint/ImporterRevision
    /// 必须与期望值同时一致才命中（任一不匹配即 miss 返回 null）。
    /// </summary>
    /// <param name="assetId">资产 ID</param>
    /// <param name="buildKey">期望构建键（null 表示不约束）</param>
    /// <param name="sourceFingerprint">期望源内容指纹（null 表示不约束）</param>
    /// <param name="importerRevision">期望导入器修订号（null 表示不约束）</param>
    /// <returns>序列化记录；未命中返回 null</returns>
    Task<AssetSerializationRecord?> LoadAsync(
        AssetId assetId,
        string? buildKey = null,
        string? sourceFingerprint = null,
        ulong? importerRevision = null);
}