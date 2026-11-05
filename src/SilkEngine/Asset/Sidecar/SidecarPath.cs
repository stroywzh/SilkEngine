using System.Security.Cryptography;

namespace SilkEngine.Asset.Sidecar;

internal static class SidecarPath
{
    public const string Extension = ".asset";

    public static string ForSource(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        return sourcePath + Extension;
    }

    /// <summary>把 sidecar 内记录的相对 source 解析成绝对路径，并拒绝逃逸。</summary>
    public static string ResolveSource(string sidecarPath, string relativeSource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sidecarPath);
        if (string.IsNullOrWhiteSpace(relativeSource))
            throw new InvalidDataException($"Missing source in sidecar '{sidecarPath}'.");
        if (Path.IsPathRooted(relativeSource))
            throw new InvalidDataException(
                $"source must be a relative file name, got '{relativeSource}' in '{sidecarPath}'."
            );

        string dir = Path.GetDirectoryName(Path.GetFullPath(sidecarPath)) ?? string.Empty;
        string combined = Path.GetFullPath(Path.Combine(dir, relativeSource));
        string prefix =
            dir.Length == 0 ? string.Empty
            : dir.EndsWith(Path.DirectorySeparatorChar) ? dir
            : dir + Path.DirectorySeparatorChar;

        if (prefix.Length > 0 && !combined.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                $"source '{relativeSource}' in '{sidecarPath}' escapes the sidecar directory."
            );
        return combined;
    }
}

internal static class ContentHash
{
    private const string Prefix = "sha256:";

    public static string Sha256(ReadOnlySpan<byte> content) =>
        Prefix + Convert.ToHexStringLower(SHA256.HashData(content));

    public static string Sha256(string path) => Sha256(File.ReadAllBytes(path));
}
