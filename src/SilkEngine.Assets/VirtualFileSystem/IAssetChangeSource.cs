namespace SilkEngine.Assets.VirtualFileSystem;

/// <summary>资产源变更分类：新增 / 内容修改（源内容指纹变化）/ 删除。</summary>
public enum AssetChangeKind
{
    /// <summary>新增：扫描中新出现的文件</summary>
    Added,

    /// <summary>修改：已存在文件的源内容指纹变化</summary>
    Modified,

    /// <summary>删除：扫描中消失的文件</summary>
    Removed,
}

/// <summary>单个资产源变更：种类 + 受影响逻辑路径（已规范化）。</summary>
/// <param name="Kind">变更分类</param>
/// <param name="LogicalPath">受影响资产的逻辑路径（相对文件服务根目录）</param>
public sealed record AssetChangeEvent(AssetChangeKind Kind, string LogicalPath);

/// <summary>
/// 一次变更探测的收敛快照：轮询/监听事件先收敛为变更列表，再由 Main 驱动对账消费；
/// 调用方（EngineLoop 低频槽）只消费快照，不接触平台级文件监视事件。
/// </summary>
/// <param name="Changes">本次探测观察到的变更列表（无变更时为空列表）</param>
public sealed record ChangeSourceResult(IReadOnlyList<AssetChangeEvent> Changes)
{
    /// <summary>无变更的空结果（单例，避免每次探测分配）</summary>
    public static ChangeSourceResult Empty { get; } = new([]);

    /// <summary>本次结果是否携带任何变更</summary>
    public bool HasChanges => Changes.Count > 0;
}

/// <summary>
/// 资产变更源抽象：以低频节奏探测资产源内容变化，收敛为幂等的变更快照。
/// 实现方不得把平台级文件监视事件直接暴露给 Pipeline/EngineLoop（事件经收敛为
/// <see cref="ChangeSourceResult"/> 后由 Main 驱动消费）；同一变更重复上报时对账可幂等吞并。
/// </summary>
public interface IAssetChangeSource
{
    /// <summary>探测一次并返回变更快照；无可报告的变更（或间隔未到）时返回 <see cref="ChangeSourceResult.Empty"/>。</summary>
    /// <returns>本次探测收敛到的变更快照</returns>
    ChangeSourceResult Poll();
}