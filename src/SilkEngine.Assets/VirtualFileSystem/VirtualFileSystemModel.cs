using SilkEngine.Assets;

namespace SilkEngine.Assets.VirtualFileSystem;

/// <summary>虚拟文件系统的数据模型，序列化和反序列化都基于这个</summary>
public sealed record VirtualFileDataModel
{
    /// <summary>全部虚拟节点快照</summary>
    public IReadOnlyList<VirtualNode> Nodes { get; init; } = [];
}

/// <summary>虚拟节点元数据模型：以 VirtualNodeId 为身份标识，节点信息均为不可变属性</summary>
public sealed record VirtualNode
{
    /// <summary>节点唯一标识；通过该标识链接资产、Meta 文件、数据库索引与硬盘上的原始文件</summary>
    public required VirtualNodeId Id { get; init; }

    /// <summary>父节点标识；根级节点为 null</summary>
    public required VirtualNodeId? ParentId { get; init; }

    /// <summary>节点类型</summary>
    public required VirtualNodeType NodeType { get; init; }

    /// <summary>节点逻辑路径（规范化后）</summary>
    public required string LogicalPath { get; init; }

    /// <summary>索引内变更序号：新增为 1，每次修改/移动递增，用于增量同步</summary>
    public required ulong Revision { get; init; }

    /// <summary>节点 Meta 信息；文件节点的 FileHash 承载扫描提供的源版本/长度标量</summary>
    public MetaDataModel? MetaData { get; init; }
}

/// <summary>节点 Meta 信息模型：文件变更追踪与源文件身份信息</summary>
public sealed record MetaDataModel
{
    /// <summary>最后编辑时间</summary>
    public DateTime LastEditTime { get; init; }

    /// <summary>文件节点的源版本/长度标量（来自扫描）；目录节点为 null</summary>
    public ulong? FileHash { get; init; }

    /// <summary>源文件 MD5；目录节点为 null</summary>
    public string? SourceMD5 { get; init; }

    /// <summary>源内容指纹（SHA-256 十六进制，来自扫描）；目录节点为 null</summary>
    public string? SourceFingerprint { get; init; }

    /// <summary>节点逻辑路径；根级节点为 string.Empty</summary>
    public string LogicPath { get; init; } = string.Empty;
}

/// <summary>节点类型</summary>
public enum VirtualNodeType : byte
{
    Directory = 0,
    File = 1,
}
