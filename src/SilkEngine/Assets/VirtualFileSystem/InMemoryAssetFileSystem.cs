namespace SilkEngine.Assets.VirtualFileSystem;

/// <summary>内存虚拟文件系统：基于逻辑路径的纯内存实现；当前仅提供路径规范化与校验，存储与 IO 由后续任务扩展</summary>
public sealed class InMemoryAssetFileSystem
{
    private const char Separator = '/';

    private readonly string[] _rootSegments;

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
}
