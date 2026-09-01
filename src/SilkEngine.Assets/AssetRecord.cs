namespace SilkEngine.Assets;

/// <summary>资产记录状态：目录中资产的加载生命周期</summary>
public enum AssetRecordState
{
    /// <summary>已登记未导入</summary>
    Pending = 0,

    /// <summary>已导入就绪</summary>
    Ready,

    /// <summary>导入失败</summary>
    Failed,

    /// <summary>已卸载</summary>
    Unloaded,
}

/// <summary>目录中的单条资产记录：绑定源节点与类型，携带稳定的资产 ID 与导入元数据</summary>
public sealed class AssetRecord
{
    /// <summary>资产唯一标识（同一源节点 + 类型组合稳定不变）</summary>
    public required AssetId AssetId { get; init; }

    /// <summary>源虚拟文件系统节点</summary>
    public required VirtualNodeId SourceNodeId { get; init; }

    /// <summary>资产类型</summary>
    public required AssetTypeId AssetTypeId { get; init; }

    /// <summary>导入器标识（按扩展名解析结果，导入后填充）</summary>
    public string? ImporterId { get; set; }

    /// <summary>源节点修订号（源变更后由导入流程更新）</summary>
    public ulong SourceRevision { get; set; }

    /// <summary>依赖的资产 ID 集合（导入时发现，当前为空）</summary>
    public IReadOnlyList<AssetId> Dependencies { get; init; } = [];

    /// <summary>记录状态</summary>
    public AssetRecordState State { get; set; }
}
