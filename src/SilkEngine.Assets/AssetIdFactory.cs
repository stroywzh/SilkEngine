using System.Security.Cryptography;
using System.Text;

namespace SilkEngine.Assets;

/// <summary>
/// 确定性资产 ID 工厂：对 UTF-8 编码的 <c>projectNamespace + "\n" + normalizedPath + "\n" + assetType</c>
/// 计算 SHA-256，取前 16 字节构造 GUID 并设置 RFC 4122 version/variant 位——
/// 相同的项目命名空间、逻辑路径与类型组合恒产生相同 AssetId（跨目录实例与进程稳定）。
/// </summary>
public static class AssetIdFactory
{
    /// <summary>生成确定性资产 ID。</summary>
    /// <param name="projectNamespace">项目命名空间（隔离不同项目的同路径资产）</param>
    /// <param name="path">资产逻辑路径（内部先经 <see cref="NormalizePath"/> 规范化）</param>
    /// <param name="assetType">资产类型标识</param>
    /// <returns>确定性资产 ID</returns>
    /// <exception cref="ArgumentException">任一输入为 null/空白时抛出</exception>
    public static AssetId Create(string projectNamespace, string path, AssetTypeId assetType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectNamespace);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetType.Value);

        var material = string.Concat(projectNamespace, "\n", NormalizePath(path), "\n", assetType.Value);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        // RFC 4122：byte[7] 高 4 位为版本号（5 = 名字空间哈希），byte[8] 高 2 位为变体（10xx）
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new AssetId(new Guid(bytes));
    }

    /// <summary>路径规范化：替换反斜杠为正斜杠、去除首尾斜杠、合并重复斜杠（大小写保持原样）。</summary>
    /// <param name="path">待规范化的路径</param>
    /// <returns>规范化后的路径</returns>
    /// <exception cref="ArgumentException">path 为 null/空白时抛出</exception>
    public static string NormalizePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var segments = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return string.Join('/', segments);
    }
}
