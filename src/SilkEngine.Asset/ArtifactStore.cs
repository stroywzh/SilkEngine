using System.IO;
using SilkEngine.Asset.Transfer;
using SilkEngine.Core.IO;

namespace SilkEngine.Asset;

/// <summary>Content-addressed store for imported payloads (<c>&lt;project&gt;/Library/artifacts</c>).</summary>
internal static class ArtifactStore
{
    /// <summary>Writes <paramref name="content"/> under its SHA-256 name; an existing artifact wins (dedup).</summary>
    public static ArtifactRef Write(ContentLayout layout, ReadOnlySpan<byte> content)
    {
        ArgumentNullException.ThrowIfNull(layout);

        string hash = ContentHash.Sha256(content);
        string target = PathFor(layout, hash);
        if (File.Exists(target))
            return new ArtifactRef { Hash = hash, Length = content.Length };

        AtomicFile.TryWriteNew(target, content);
        return new ArtifactRef { Hash = hash, Length = content.Length };
    }

    /// <summary>Reads the artifact's bytes; false when missing, unreadable or a different length than recorded.</summary>
    public static bool TryRead(
        ContentLayout layout,
        ArtifactRef artifact,
        out byte[] content
    )
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(artifact);

        try
        {
            if (!TryReadPath(PathFor(layout, artifact.Hash), out byte[] bytes))
            {
                content = Array.Empty<byte>();
                return false;
            }

            if (bytes.Length != artifact.Length)
            {
                content = Array.Empty<byte>();
                return false;
            }

            content = bytes;
            return true;
        }
        catch (Exception error)
            when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            content = Array.Empty<byte>();
            return false;
        }
    }

    /// <summary>Reads an artifact file by path without length validation; the caller checks it against the header.</summary>
    internal static bool TryReadPath(string artifactPath, out byte[] content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactPath);

        try
        {
            if (!File.Exists(artifactPath))
            {
                content = Array.Empty<byte>();
                return false;
            }

            content = File.ReadAllBytes(artifactPath);
            return true;
        }
        catch (Exception error)
            when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            content = Array.Empty<byte>();
            return false;
        }
    }

    /// <summary>True when the artifact file named by <paramref name="artifact"/> exists.</summary>
    public static bool Exists(ContentLayout layout, ArtifactRef artifact)
    {
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(artifact);

        try
        {
            return File.Exists(PathFor(layout, artifact.Hash));
        }
        catch (Exception error)
            when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return false;
        }
    }

    /// <summary>Artifact path for <paramref name="hash"/> under the project layout.</summary>
    internal static string PathFor(ContentLayout layout, string hash)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (!ContentHash.TryGetHex(hash, out string hex))
            throw new InvalidDataException(
                $"Artifact hash '{hash}' must be a '{ContentHash.Prefix}' SHA-256 hex digest."
            );

        return Path.Combine(
            layout.ArtifactsRoot,
            hex[..2],
            hex[2..4],
            hex + AssetConstants.ArtifactExtensionName
        );
    }
}
