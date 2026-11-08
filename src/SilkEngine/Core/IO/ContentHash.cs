using System.IO;
using System.Security.Cryptography;

namespace SilkEngine.Core.IO;

/// <summary>Content addressing shared by the catalog, sidecars and artifacts.</summary>
internal static class ContentHash
{
    internal const string Prefix = "sha256:";

    internal static string Sha256(ReadOnlySpan<byte> content) =>
        Prefix + Convert.ToHexStringLower(SHA256.HashData(content));

    internal static string Sha256(string path) => Sha256(File.ReadAllBytes(path));

    /// <summary>Validates a <c>sha256:&lt;lower hex&gt;</c> digest and returns its hex part.</summary>
    internal static bool TryGetHex(string? hash, out string hex)
    {
        hex = string.Empty;
        if (
            string.IsNullOrWhiteSpace(hash)
            || !hash.StartsWith(Prefix, StringComparison.Ordinal)
        )
            return false;

        string candidate = hash[Prefix.Length..];
        if (candidate.Length != 64 || !IsLowerHex(candidate))
            return false;

        hex = candidate;
        return true;
    }

    private static bool IsLowerHex(string value)
    {
        foreach (char c in value)
        {
            if (c is not ((>= '0' and <= '9') or (>= 'a' and <= 'f')))
                return false;
        }

        return true;
    }
}
