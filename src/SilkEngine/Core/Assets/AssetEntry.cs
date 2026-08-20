namespace SilkEngine.Core.Assets;

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

/// <summary>资产缓存条目：GUID 键 + 数据 + 引用计数 + 状态 + 等待者</summary>
public sealed class AssetEntry
{
    /// <summary>资产标识（路径稳定哈希）</summary>
    public required Guid Guid { get; init; }

    /// <summary>已加载资产数据；未完成/失败时为 null</summary>
    public IAsset? Data { get; set; }

    /// <summary>引用计数（+1/-1 仅经 AssetManager 闭环修改，外部不可绕过）</summary>
    public int RefCount { get; internal set; }

    /// <summary>当前状态</summary>
    public AssetState State { get; set; } = AssetState.Loading;

    /// <summary>发起本次加载的首个请求（合并去重依据）</summary>
    internal IAssetRequest? Pending { get; set; }

    /// <summary>加载期间登记的等待者，帧末统一唤醒</summary>
    internal List<IAssetRequest> Awaiters { get; } = new();
}

/// <summary>工作线程产出的加载结果，帧末由主线程拾取</summary>
internal readonly record struct AssetLoadResult
{
    public AssetLoadResult(Guid guid, IAsset? asset, Exception? error)
    {
        Guid = guid;
        Asset = asset;
        Error = error;
    }

    /// <summary>资产 GUID</summary>
    public Guid Guid { get; init; }

    /// <summary>已加载资产；失败为 null</summary>
    public IAsset? Asset { get; init; }

    /// <summary>加载异常；成功为 null</summary>
    public Exception? Error { get; init; }
}
