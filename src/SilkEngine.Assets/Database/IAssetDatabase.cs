namespace SilkEngine.Assets.Database;

/// <summary>资产数据库资产行记录：资产标识、逻辑路径、类型与源指纹/修订</summary>
/// <param name="AssetId">资产唯一标识</param>
/// <param name="LogicalPath">资产逻辑路径（数据库内唯一）</param>
/// <param name="AssetType">资产类型标识</param>
/// <param name="SourceFingerprint">源内容指纹</param>
/// <param name="SourceRevision">源修订号</param>
public sealed record AssetDbAssetRecord(
    AssetId AssetId,
    string LogicalPath,
    AssetTypeId AssetType,
    string SourceFingerprint,
    ulong SourceRevision);

/// <summary>资产数据库构建行记录：BuildKey 到缓存产物与源指纹的映射</summary>
/// <param name="AssetId">所属资产唯一标识</param>
/// <param name="BuildKey">构建键（去重主键）</param>
/// <param name="CachePath">构建缓存产物路径</param>
/// <param name="SourceFingerprint">构建时的源内容指纹</param>
public sealed record AssetDbBuildRecord(
    AssetId AssetId,
    string BuildKey,
    string CachePath,
    string SourceFingerprint);

/// <summary>资产数据库契约：SQLite 持久化的资产/构建元数据存取与整体快照</summary>
internal interface IAssetDatabase : IAsyncDisposable
{
    /// <summary>初始化数据库：启用 WAL、建表并记录迁移版本；文件损坏时改名备份并抛 <see cref="AssetDatabaseCorruptException"/></summary>
    /// <param name="cancellationToken">取消令牌</param>
    ValueTask InitializeAsync(CancellationToken cancellationToken);

    /// <summary>按逻辑路径查询资产记录</summary>
    /// <param name="logicalPath">资产逻辑路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>命中的资产记录；不存在时返回 null</returns>
    ValueTask<AssetDbAssetRecord?> GetAssetAsync(string logicalPath, CancellationToken cancellationToken);

    /// <summary>按构建键查询构建记录</summary>
    /// <param name="buildKey">构建键</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>命中的构建记录；不存在时返回 null</returns>
    ValueTask<AssetDbBuildRecord?> GetBuildAsync(string buildKey, CancellationToken cancellationToken);

    /// <summary>插入或更新资产记录（按 AssetId 幂等）</summary>
    /// <param name="record">待写入的资产记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    ValueTask UpsertAssetAsync(AssetDbAssetRecord record, CancellationToken cancellationToken);

    /// <summary>插入或更新构建记录（按 BuildKey 幂等）</summary>
    /// <param name="record">待写入的构建记录</param>
    /// <param name="cancellationToken">取消令牌</param>
    ValueTask UpsertBuildAsync(AssetDbBuildRecord record, CancellationToken cancellationToken);

    /// <summary>捕获全部资产、文件节点、依赖边与构建记录的不可变快照</summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前数据库内容的整体快照</returns>
    ValueTask<AssetDatabaseSnapshot> CaptureSnapshotAsync(CancellationToken cancellationToken);
}
