using SilkEngine.Assets.Importer;

namespace SilkEngine.Assets;

/// <summary>
/// 资产构建键：去重与缓存的唯一键。
/// 导入设置中影响输出的数据（源修订、导入器修订、目标配置）必须在键中体现，不能只用路径或 AssetId。
/// </summary>
/// <param name="AssetId">资产标识</param>
/// <param name="AssetType">资产类型</param>
/// <param name="SourceRevision">源修订（请求时从目录捕获）</param>
/// <param name="ImporterRevision">导入器修订/设置指纹</param>
/// <param name="TargetProfile">目标配置（当前为空串）</param>
public readonly record struct AssetBuildKey(
    AssetId AssetId,
    AssetTypeId AssetType,
    ulong SourceRevision,
    ulong ImporterRevision,
    string TargetProfile);

/// <summary>Pipeline 结果状态</summary>
public enum AssetPipelineResultState
{
    /// <summary>构建成功</summary>
    Succeeded,

    /// <summary>构建失败（Error 携带原始异常）</summary>
    Failed,
}

/// <summary>
/// Pipeline 结果：构建键 + Payload + 依赖 + 状态 + 错误。
/// 不可变，不携带可变 <see cref="AssetEntry"/>；由 Worker 生成、经 FrameCommit 交 AssetManager 应用。
/// </summary>
/// <param name="Key">构建键</param>
/// <param name="Payload">导入生成的不可变载荷；失败为 null</param>
/// <param name="Dependencies">依赖句柄列表</param>
/// <param name="State">结果状态</param>
/// <param name="Error">失败原因；成功为 null</param>
public sealed record AssetPipelineResult(
    AssetBuildKey Key,
    IAssetPayload? Payload,
    IReadOnlyList<UntypedAssetHandle> Dependencies,
    AssetPipelineResultState State,
    Exception? Error);

/// <summary>
/// 导入结果：Payload + 依赖句柄 + 导入器修订号。
/// 导入器只从源数据生成 <see cref="IAssetPayload"/>，不创建 GPU 对象、Scene 组件或运行时实例。
/// </summary>
/// <param name="Payload">导入生成的载荷</param>
/// <param name="Dependencies">本次导入发现的依赖句柄</param>
/// <param name="ImporterRevision">导入器修订号（输出变化时递增，供过期校验）</param>
public sealed record AssetImportResult(
    IAssetPayload Payload,
    IReadOnlyList<UntypedAssetHandle> Dependencies,
    ulong ImporterRevision);

/// <summary>导入上下文：源路径与导入设置</summary>
/// <param name="Path">源逻辑路径</param>
/// <param name="Settings">导入设置</param>
public sealed record AssetImportContext(string Path, ImportSettings? Settings);

/// <summary>
/// 过期结果异常：结果在发布前源/导入器修订已变更，不得写入缓存。
/// </summary>
public sealed class AssetStaleResultException : Exception
{
    /// <summary>创建过期结果异常</summary>
    /// <param name="key">过期结果对应的构建键</param>
    public AssetStaleResultException(AssetBuildKey key)
        : base($"Asset result for '{key.AssetId}' (source revision {key.SourceRevision}) is stale and was not published.")
    {
        Key = key;
    }

    /// <summary>过期结果对应的构建键</summary>
    public AssetBuildKey Key { get; }
}
