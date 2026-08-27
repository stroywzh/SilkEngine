namespace SilkEngine.Assets.VirtualFileSystem;

/// <summary>内存虚拟文件系统：基于逻辑路径的纯内存实现，提供路径规范化、文件快照写入与只读访问</summary>
public sealed class InMemoryAssetFileSystem : IAssetFileSystem
{
    private const char Separator = '/';

    private readonly string[] _rootSegments;
    private readonly Dictionary<string, FileEntry> _files = new(StringComparer.Ordinal);

    private sealed record FileEntry(byte[] Content, ulong Version, DateTime LastWriteTimeUtc);

    /// <summary>创建以指定逻辑路径为根的内存文件系统</summary>
    /// <param name="rootPath">根逻辑路径</param>
    /// <exception cref="ArgumentException">rootPath 为 null 或空白时抛出</exception>
    public InMemoryAssetFileSystem(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("根路径不能为空或空白。", nameof(rootPath));
        _rootSegments = rootPath.Replace('\\', Separator)
            .Split(Separator, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>校验并规范化逻辑路径：拒绝 null/空白、绝对路径与越出根目录的 .. 段</summary>
    /// <param name="path">待规范化的逻辑路径（相对根目录）</param>
    /// <returns>规范化后的逻辑路径（分隔符统一为 '/'，已消除 . 与可折叠的 .. 段）</returns>
    /// <exception cref="ArgumentException">path 为 null/空白、绝对路径或 .. 越出根目录时抛出</exception>
    public string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("逻辑路径不能为空或空白。", nameof(path));
        if (Path.IsPathRooted(path))
            throw new ArgumentException("逻辑路径不能是绝对路径。", nameof(path));

        var segments = path.Replace('\\', Separator)
            .Split(Separator, StringSplitOptions.RemoveEmptyEntries);
        var stack = new List<string>(_rootSegments);
        foreach (var segment in segments)
        {
            if (segment == ".")
                continue;
            if (segment == "..")
            {
                if (stack.Count == _rootSegments.Length)
                    throw new ArgumentException("逻辑路径越出根目录。", nameof(path));
                stack.RemoveAt(stack.Count - 1);
                continue;
            }
            stack.Add(segment);
        }
        return string.Join(Separator, stack.Skip(_rootSegments.Length));
    }

    /// <summary>写入文件快照：相对根路径校验沿用 Normalize 语义；已存在路径覆盖并递增版本</summary>
    /// <param name="path">逻辑路径（相对根目录）</param>
    /// <param name="content">文件内容（内部复制保存，调用方后续修改不影响快照）</param>
    /// <exception cref="ArgumentException">path 为 null/空白、绝对路径或 .. 越出根目录时抛出</exception>
    public void Add(string path, byte[] content)
    {
        var normalized = Normalize(path);
        var version = _files.TryGetValue(normalized, out var entry) ? entry.Version : 0UL;
        _files[normalized] = new FileEntry((byte[])content.Clone(), version + 1, DateTime.UtcNow);
    }

    /// <summary>判断指定逻辑路径对应的文件是否存在</summary>
    /// <param name="path">逻辑路径（相对根目录）</param>
    /// <returns>文件存在时为 true</returns>
    /// <exception cref="ArgumentException">path 为 null/空白、绝对路径或 .. 越出根目录时抛出</exception>
    public bool Exists(string path) => _files.ContainsKey(Normalize(path));

    /// <summary>异步读取文件内容</summary>
    /// <param name="path">逻辑路径（相对根目录）</param>
    /// <returns>文件内容的只读内存视图</returns>
    /// <exception cref="ArgumentException">path 为 null/空白、绝对路径或 .. 越出根目录时抛出</exception>
    /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
    public ValueTask<ReadOnlyMemory<byte>> ReadAsync(string path)
    {
        var normalized = Normalize(path);
        if (!_files.TryGetValue(normalized, out var entry))
            throw new FileNotFoundException($"文件不存在：{normalized}", normalized);
        return ValueTask.FromResult<ReadOnlyMemory<byte>>(entry.Content);
    }

    /// <summary>异步读取文件元数据</summary>
    /// <param name="path">逻辑路径（相对根目录）</param>
    /// <returns>文件元数据（长度/版本/最后写入时间）</returns>
    /// <exception cref="ArgumentException">path 为 null/空白、绝对路径或 .. 越出根目录时抛出</exception>
    /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
    public ValueTask<FileMetadata> GetMetadataAsync(string path)
    {
        var normalized = Normalize(path);
        if (!_files.TryGetValue(normalized, out var entry))
            throw new FileNotFoundException($"文件不存在：{normalized}", normalized);
        return ValueTask.FromResult(new FileMetadata(entry.Content.LongLength, entry.Version, entry.LastWriteTimeUtc));
    }
}
