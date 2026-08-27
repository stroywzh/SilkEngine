//虚拟文件系统的数据模型，序列化和反序列化都基于这个
namespace SilkEngine.Assets.VirtualFileSystem;

//TODO : 提取整体序列化Model，到统一的序列化/反序列化基类(等到Asset管线完成之后)
// 虚拟节点的序列化存储模型
public class VirtualFileDataModel
{
    public List<VirtualNode> Nodes;
}

/// <summary>
/// 虚拟节点
/// </summary>
public class VirtualNode
{
    // 节点内部id,通过该id来作为唯一标识,
    // 通过该标识链接 Asset/Meta文件/DataBase索引/硬盘上的原始文件
    public Guid InternalGuid { get; set; }

    // 父节点id
    // TIP: 指定根目录的节点为 null
    public Guid? ParentGuid { get; set; } = null;
    public VirtualNodeType NodeType = VirtualNodeType.Directory;

    public MetaDataModel MetaData { get; set; }
}

/// <summary>
/// 节点Meta信息模型
/// </summary>
public record MetaDataModel
{
    public DateTime LastEditTime;

    // nodeType is File 才有这个hash和MD5，Dir默认都是null
    public ulong? FileHash = null;
    public string? SourceMD5 = null;

    // 根目录节点为string.Empty
    public string LogicPath = string.Empty;
}

/// <summary>
/// 节点类型
/// </summary>
public enum VirtualNodeType : byte
{
    Directory = 0,
    File = 1,
}
