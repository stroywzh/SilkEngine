using System.Buffers;
using System.Security.Cryptography;

namespace SilkEngine.Assets.VirtualFileSystem;

/// <summary>
/// 磁盘文件服务：BCL 文件 IO 实现的生产默认占位，将逻辑路径映射到根目录下物理文件。
/// 路径规范化语义与 InMemoryAssetFileSystem 一致（拒绝绝对路径/越界 ..，分隔符统一 '/'）；
/// 根路径既是逻辑命名空间前缀，也是物理基目录。
/// </summary>
public sealed class DiskAssetFileSystem : IAssetFileSystem
{
    private const char Separator = '/';

    // 源内容指纹的流式读取缓冲大小
    private const int SourceHashBufferSize = 8192;

    private readonly string _root;
    private readonly string[] _rootSegments;

    /// <summary>创建以指定目录为根的磁盘文件服务（目录可不存在；读写时按需解析）</summary>
    /// <param name="rootPath">物理基目录（逻辑路径相对其解析）</param>
    /// <exception cref="ArgumentException">rootPath 为 null 或空白时抛出</exception>
    public DiskAssetFileSystem(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            throw new ArgumentException("根路径不能为空或空白。", nameof(rootPath));
        _root = Path.GetFullPath(rootPath);
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

    /// <summary>判断指定逻辑路径对应的物理文件是否存在</summary>
    /// <param name="path">逻辑路径（相对根目录）</param>
    /// <returns>文件存在时为 true</returns>
    /// <exception cref="ArgumentException">path 为 null/空白、绝对路径或 .. 越出根目录时抛出</exception>
    public bool Exists(string path) => File.Exists(ToPhysical(Normalize(path)));

    /// <summary>异步读取文件内容</summary>
    /// <param name="path">逻辑路径（相对根目录）</param>
    /// <returns>文件内容的只读内存视图</returns>
    /// <exception cref="ArgumentException">path 为 null/空白、绝对路径或 .. 越出根目录时抛出</exception>
    /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
    public async ValueTask<ReadOnlyMemory<byte>> ReadAsync(string path)
    {
        var normalized = Normalize(path);
        var physical = ToPhysical(normalized);
        if (!File.Exists(physical))
            throw new FileNotFoundException($"文件不存在：{normalized}", normalized);
        return await File.ReadAllBytesAsync(physical);
    }

    /// <summary>异步读取文件元数据（Version 以最后写入时间刻度为占位标量；真实版本追踪由后续扫描任务接入）</summary>
    /// <param name="path">逻辑路径（相对根目录）</param>
    /// <returns>文件元数据（长度/版本/最后写入时间）</returns>
    /// <exception cref="ArgumentException">path 为 null/空白、绝对路径或 .. 越出根目录时抛出</exception>
    /// <exception cref="FileNotFoundException">文件不存在时抛出</exception>
    public ValueTask<FileMetadata> GetMetadataAsync(string path)
    {
        var normalized = Normalize(path);
        var physical = ToPhysical(normalized);
        if (!File.Exists(physical))
            throw new FileNotFoundException($"文件不存在：{normalized}", normalized);
        var info = new FileInfo(physical);
        return ValueTask.FromResult(
            new FileMetadata(info.Length, unchecked((ulong)info.LastWriteTimeUtc.Ticks), info.LastWriteTimeUtc));
    }

    /// <summary>
    /// 启动扫描：递归枚举根目录下全部文件与目录，生成扫描结果（逻辑路径相对根目录，分隔符统一 '/'）；
    /// 文件条目携带源内容 SHA-256 指纹（流式读取，不缓存完整字节），目录条目不计算内容哈希。
    /// 根目录不存在时返回空扫描结果。
    /// </summary>
    /// <returns>本次扫描观察到的全部条目</returns>
    public ScanResult Scan()
    {
        var files = new List<ScanFile>();
        if (Directory.Exists(_root))
            ScanDirectory(_root, files);
        return ScanResult.FromFiles(files);
    }

    private void ScanDirectory(string physicalDir, List<ScanFile> files)
    {
        foreach (var dir in Directory.GetDirectories(physicalDir))
        {
            files.Add(ScanFile.Directory(ToLogical(dir)));
            ScanDirectory(dir, files);
        }
        foreach (var file in Directory.GetFiles(physicalDir))
        {
            var info = new FileInfo(file);
            files.Add(ScanFile.File(
                ToLogical(file),
                unchecked((ulong)info.LastWriteTimeUtc.Ticks),
                sourceFingerprint: ComputeSourceFingerprint(file)));
        }
    }

    /// <summary>
    /// 流式计算源内容 SHA-256（小写十六进制）：固定 8KB 租借缓冲增量哈希，
    /// 不保留任何内容字节；分配面收敛到句柄/哈希器/结果字符串，避免扫描期分配噪声。
    /// </summary>
    private static string ComputeSourceFingerprint(string physicalPath)
    {
        using var handle = File.OpenHandle(physicalPath, options: FileOptions.SequentialScan);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(SourceHashBufferSize);
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long offset = 0;
            int read;
            while ((read = RandomAccess.Read(handle, buffer, offset)) > 0)
            {
                hash.AppendData(buffer.AsSpan(0, read));
                offset += read;
            }
            Span<byte> digest = stackalloc byte[32];
            hash.GetHashAndReset(digest);
            return string.Create(digest.Length * 2, digest, static (span, value) =>
            {
                const string HexDigits = "0123456789abcdef";
                for (int i = 0; i < value.Length; i++)
                {
                    span[i * 2] = HexDigits[value[i] >> 4];
                    span[i * 2 + 1] = HexDigits[value[i] & 0x0F];
                }
            });
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private string ToLogical(string physical)
    {
        var relative = Path.GetRelativePath(_root, physical);
        return relative == "." ? string.Empty : relative.Replace(Path.DirectorySeparatorChar, Separator);
    }

    private string ToPhysical(string normalized) =>
        Path.Combine(_root, normalized.Replace(Separator, Path.DirectorySeparatorChar));
}
