namespace SilkEngine.Assets.Database;

/// <summary>文件节点记录：虚拟文件系统节点与其逻辑路径的持久化镜像</summary>
/// <param name="NodeId">虚拟节点唯一标识</param>
/// <param name="LogicalPath">节点逻辑路径（数据库内唯一）</param>
public sealed record AssetDbFileNodeRecord(VirtualNodeId NodeId, string LogicalPath);

/// <summary>依赖边记录：资产对其依赖逻辑路径的引用</summary>
/// <param name="AssetId">依赖方资产</param>
/// <param name="DependsOnPath">被依赖的逻辑路径</param>
public sealed record AssetDbDependencyRecord(AssetId AssetId, string DependsOnPath);

/// <summary>资产数据库不可变快照：资产、文件节点、依赖边与构建记录的整体视图</summary>
/// <param name="Assets">全部资产记录</param>
/// <param name="FileNodes">全部文件节点记录</param>
/// <param name="Dependencies">全部依赖边记录</param>
/// <param name="Builds">全部构建记录</param>
public sealed record AssetDatabaseSnapshot(
    AssetDbAssetRecord[] Assets,
    AssetDbFileNodeRecord[] FileNodes,
    AssetDbDependencyRecord[] Dependencies,
    AssetDbBuildRecord[] Builds);
