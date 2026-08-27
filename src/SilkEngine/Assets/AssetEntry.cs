namespace SilkEngine.Assets;

/// <summary>资产生命周期状态</summary>
public enum AssetState
{
    /// <summary>加载中（已登记或已调度）</summary>
    Loading,

    /// <summary>加载完成，可安全访问</summary>
    Ready,

    /// <summary>加载失败</summary>
    Failed,

    /// <summary>已卸载（RefCount==0 帧末迁移；释放前重新引用可恢复 Ready）</summary>
    Unloaded,
}

/// <summary>资产缓存条目：AssetId 键 + 数据 + 引用计数 + 状态 + 加载修订/操作令牌 + 等待者</summary>
public sealed class AssetEntry
{
    /// <summary>资产标识（目录稳定分配）</summary>
    public required AssetId AssetId { get; init; }

    /// <summary>已加载资产数据（IAssetPayload 或过渡期 IAsset 兼容实例）；未完成/失败时为 null</summary>
    public object? Data { get; set; }

    /// <summary>引用计数（+1/-1 仅经 AssetManager 闭环修改，外部不可绕过）</summary>
    public int RefCount { get; internal set; }

    /// <summary>当前状态</summary>
    public AssetState State { get; set; } = AssetState.Loading;

    /// <summary>发起本次加载的首个请求（合并去重依据）</summary>
    internal IAssetRequest? Pending { get; set; }

    /// <summary>加载期间登记的等待者，帧末统一唤醒</summary>
    internal List<IAssetRequest> Awaiters { get; } = new();

    /// <summary>本条目的数据/进行中操作对应的源修订号（缓存命中只接受与目录当前修订一致）</summary>
    internal ulong SourceRevision { get; set; }

    /// <summary>当前进行中后台操作的令牌（帧末据此识别过期/被取代的结果）</summary>
    internal ulong OperationToken { get; set; }

    /// <summary>源逻辑路径（调度与过期重载所需）</summary>
    internal string? SourcePath { get; set; }
}

/// <summary>工作线程产出的加载结果，帧末由主线程拾取；携带 AssetId + 源修订 + 操作令牌供过期校验</summary>
internal readonly record struct AssetLoadResult
{
    public AssetLoadResult(
        AssetId assetId,
        ulong sourceRevision,
        ulong operationToken,
        IAsset? asset,
        Exception? error)
    {
        AssetId = assetId;
        SourceRevision = sourceRevision;
        OperationToken = operationToken;
        Asset = asset;
        Error = error;
    }

    /// <summary>资产标识</summary>
    public AssetId AssetId { get; init; }

    /// <summary>本次操作调度时捕获的源修订号</summary>
    public ulong SourceRevision { get; init; }

    /// <summary>本次后台操作的令牌</summary>
    public ulong OperationToken { get; init; }

    /// <summary>已加载资产；失败为 null</summary>
    public IAsset? Asset { get; init; }

    /// <summary>加载异常；成功为 null</summary>
    public Exception? Error { get; init; }
}
