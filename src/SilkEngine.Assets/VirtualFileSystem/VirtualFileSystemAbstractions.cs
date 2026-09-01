using SilkEngine.Assets;

namespace SilkEngine.Assets.VirtualFileSystem;

/// <summary>文件元数据：长度、版本与最后写入时间（UTC）</summary>
/// <param name="Length">文件字节长度</param>
/// <param name="Version">文件版本；每次覆盖写入递增</param>
/// <param name="LastWriteTimeUtc">最后写入时间（UTC）</param>
public readonly record struct FileMetadata(long Length, ulong Version, DateTime LastWriteTimeUtc);

/// <summary>资产文件系统抽象：基于逻辑路径的只读文件访问；实现方负责路径规范化与越界校验</summary>
public interface IAssetFileSystem
{
    /// <summary>校验并规范化逻辑路径：拒绝 null/空白、绝对路径与越出根目录的 .. 段</summary>
    /// <param name="path">待规范化的逻辑路径（相对根目录）</param>
    /// <returns>规范化后的逻辑路径</returns>
    /// <exception cref="ArgumentException">path 为 null/空白、绝对路径或 .. 越出根目录时抛出</exception>
    string Normalize(string path);

    /// <summary>判断指定逻辑路径对应的文件是否存在</summary>
    /// <param name="path">逻辑路径（相对根目录）</param>
    /// <returns>文件存在时为 true</returns>
    bool Exists(string path);

    /// <summary>异步读取文件内容</summary>
    /// <param name="path">逻辑路径（相对根目录）</param>
    /// <returns>文件内容的只读内存视图</returns>
    /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
    ValueTask<ReadOnlyMemory<byte>> ReadAsync(string path);

    /// <summary>异步读取文件元数据</summary>
    /// <param name="path">逻辑路径（相对根目录）</param>
    /// <returns>文件元数据（长度/版本/最后写入时间）</returns>
    /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
    ValueTask<FileMetadata> GetMetadataAsync(string path);
}

/// <summary>扫描条目：一次扫描中观察到的单个文件或目录</summary>
public sealed record ScanFile
{
    /// <summary>逻辑路径（调用方保证已规范化）</summary>
    public required string LogicalPath { get; init; }

    /// <summary>节点类型</summary>
    public required VirtualNodeType NodeType { get; init; }

    /// <summary>源版本/长度标量；仅文件使用，用于识别内容变化</summary>
    public ulong Version { get; init; }

    /// <summary>源内容指纹（SHA-256 十六进制）；仅文件携带，目录为 null</summary>
    public string? SourceFingerprint { get; init; }

    /// <summary>移动身份提示：上一位置逻辑路径；为 null 时扫描不携带旧身份</summary>
    public string? PreviousPath { get; init; }

    /// <summary>创建目录扫描条目</summary>
    /// <param name="logicalPath">目录逻辑路径</param>
    /// <returns>目录扫描条目</returns>
    public static ScanFile Directory(string logicalPath) => new()
    {
        LogicalPath = logicalPath,
        NodeType = VirtualNodeType.Directory,
    };

    /// <summary>创建文件扫描条目</summary>
    /// <param name="logicalPath">文件逻辑路径</param>
    /// <param name="version">源版本/长度标量</param>
    /// <param name="previousPath">移动身份提示：上一位置逻辑路径，可为 null</param>
    /// <param name="sourceFingerprint">源内容指纹（SHA-256 十六进制），可为 null</param>
    /// <returns>文件扫描条目</returns>
    public static ScanFile File(string logicalPath, ulong version, string? previousPath = null, string? sourceFingerprint = null) => new()
    {
        LogicalPath = logicalPath,
        NodeType = VirtualNodeType.File,
        Version = version,
        PreviousPath = previousPath,
        SourceFingerprint = sourceFingerprint,
    };
}

/// <summary>一次扫描的完整结果：本次扫描观察到的全部条目</summary>
public sealed record ScanResult
{
    /// <summary>本次扫描观察到的全部条目（按扫描顺序）</summary>
    public required IReadOnlyList<ScanFile> Files { get; init; }

    /// <summary>由条目集合构建扫描结果</summary>
    /// <param name="files">扫描条目集合</param>
    /// <returns>扫描结果</returns>
    public static ScanResult FromFiles(IReadOnlyCollection<ScanFile> files) => new() { Files = files.ToArray() };
}

/// <summary>索引增量变更类型</summary>
public enum VirtualChangeKind
{
    /// <summary>新增：索引中不存在的新节点</summary>
    Added,

    /// <summary>修改：已存在节点的内容/类型变化</summary>
    Modified,

    /// <summary>删除：扫描中消失的节点</summary>
    Removed,

    /// <summary>移动：扫描携带旧身份且成功定位的路径变化</summary>
    Moved,
}

/// <summary>索引增量变更：描述单个节点的新增/修改/删除/移动</summary>
/// <param name="Kind">变更类型</param>
/// <param name="NodeId">受影响节点 ID</param>
/// <param name="LogicalPath">变更后的逻辑路径</param>
/// <param name="PreviousPath">移动场景下的原逻辑路径；其他场景为 null</param>
public sealed record VirtualChange(VirtualChangeKind Kind, VirtualNodeId NodeId, string LogicalPath, string? PreviousPath = null);

/// <summary>Apply 结果：本次应用产生的全部增量变更</summary>
/// <param name="Changes">增量变更列表（新增/修改/移动按扫描顺序，删除在最后）</param>
public sealed record VirtualIndexApplyResult(IReadOnlyList<VirtualChange> Changes);

/// <summary>虚拟文件索引抽象：以逻辑路径与节点 ID 双索引保存扫描结果，支持增量查询与更新</summary>
public interface IVirtualFileIndex
{
    /// <summary>按逻辑路径查找节点</summary>
    /// <param name="logicalPath">逻辑路径（扫描时使用的规范化路径）</param>
    /// <param name="node">命中的节点；未命中为 null</param>
    /// <returns>命中时为 true</returns>
    bool TryGet(string logicalPath, out VirtualNode? node);

    /// <summary>按节点 ID 查找节点</summary>
    /// <param name="id">节点 ID</param>
    /// <param name="node">命中的节点；未命中为 null</param>
    /// <returns>命中时为 true</returns>
    bool TryGet(VirtualNodeId id, out VirtualNode? node);

    /// <summary>枚举指定目录的直接子节点；目录不存在或不是目录时返回空序列</summary>
    /// <param name="directoryPath">目录逻辑路径</param>
    /// <returns>直接子节点序列</returns>
    IEnumerable<VirtualNode> EnumerateChildren(string directoryPath);

    /// <summary>应用一次扫描结果，返回增量变更；重复提交相同扫描不产生变更</summary>
    /// <param name="scan">扫描结果</param>
    /// <returns>本次应用产生的增量变更</returns>
    VirtualIndexApplyResult Apply(ScanResult scan);
}
