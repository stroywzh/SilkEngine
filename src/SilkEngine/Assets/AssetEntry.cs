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

    /// <summary>已卸载（无持有者帧末驱逐；重新引用可恢复 Ready）</summary>
    Unloaded,
}

/// <summary>资产缓存条目：AssetId 键 + Payload + 引用计数 + 状态 + 源修订</summary>
public sealed class AssetEntry
{
    /// <summary>资产标识（目录稳定分配）</summary>
    public required AssetId AssetId { get; init; }

    /// <summary>已加载载荷（IAssetPayload 或过渡期 IAsset 兼容实例）；未完成/失败时为 null</summary>
    public object? Payload { get; set; }

    /// <summary>引用计数（+1/-1 仅经 AssetManager 闭环修改，外部不可绕过；过渡期遗留 API）</summary>
    public int RefCount { get; internal set; }

    /// <summary>当前状态</summary>
    public AssetState State { get; set; } = AssetState.Loading;

    /// <summary>本条目的数据/进行中操作对应的源修订号（缓存命中只接受与目录当前修订一致）</summary>
    internal ulong SourceRevision { get; set; }
}
