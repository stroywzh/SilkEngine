using System.IO;
using SilkEngine.Asset.Transfer;

namespace SilkEngine.Asset;

/// <summary>Content-addressed store for imported payloads (<c>&lt;project&gt;/Library/artifacts</c>).</summary>
/// <remarks>The sidecar lives below <c>Assets/</c>, so the project root is that folder's parent; the
/// root is derived here and stays non-configurable until a later phase needs it.</remarks>
internal static class ArtifactStore
{
    private const string HashPrefix = "sha256:";

    /// <summary>Writes <paramref name="content"/> under its SHA-256 name; an existing artifact wins (dedup).</summary>
    public static ArtifactRef Write(string sidecarPath, ReadOnlySpan<byte> content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sidecarPath);

        string hash = ContentHash.Sha256(content);
        string target = ResolvePath(sidecarPath, hash);
        if (File.Exists(target))
            return new ArtifactRef { Hash = hash, Length = content.Length };

        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        // Same-directory temp file so the rename below stays atomic on one volume.
        string temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (
                var stream = new FileStream(
                    temp,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None
                )
            )
                stream.Write(content);

            try
            {
                File.Move(temp, target, overwrite: false);
            }
            catch (IOException) when (File.Exists(target))
            {
                // Another importer materialized the same content first: the target wins.
            }
        }
        finally
        {
            try
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }

        return new ArtifactRef { Hash = hash, Length = content.Length };
    }

    /// <summary>Reads the artifact's bytes; false when missing, unreadable or a different length than recorded.</summary>
    public static bool TryRead(string sidecarPath, ArtifactRef artifact, out byte[] content)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        try
        {
            if (!TryReadPath(ResolvePath(sidecarPath, artifact.Hash), out byte[] bytes))
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
    public static bool Exists(string sidecarPath, ArtifactRef artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        try
        {
            return File.Exists(ResolvePath(sidecarPath, artifact.Hash));
        }
        catch (Exception error)
            when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return false;
        }
    }

    /// <summary>Artifact path for <paramref name="hash"/> under <paramref name="projectRoot"/>.</summary>
    internal static string PathForProject(string projectRoot, string hash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);

        string hex = ValidateHash(hash);
        string artifactsRoot = Path.GetFullPath(
            Path.Combine(
                Path.GetFullPath(projectRoot),
                AssetConstants.LibraryFolderName,
                AssetConstants.ArtifactsFolderName
            )
        );

        return Path.Combine(
            artifactsRoot,
            hex[..2],
            hex[2..4],
            hex + AssetConstants.ArtifactExtensionName
        );
    }

    /// <summary>Project root of a sidecar: the parent of the sidecar's <c>Assets/</c> folder.</summary>
    internal static string ProjectRootFor(string sidecarPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sidecarPath);

        string directory =
            Path.GetDirectoryName(Path.GetFullPath(sidecarPath))
            ?? throw new InvalidDataException($"Sidecar path '{sidecarPath}' has no directory.");

        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        // Walk up to the project's Assets/ folder (sidecars may live in nested source folders).
        for (DirectoryInfo? current = new(directory); current is not null; current = current.Parent)
        {
            if (!string.Equals(current.Name, AssetConstants.AssetsFolderName, comparison))
                continue;

            return current.Parent?.FullName
                ?? throw new InvalidDataException(
                    $"Sidecar path '{sidecarPath}' has no project root above its '{AssetConstants.AssetsFolderName}' folder."
                );
        }

        // No Assets/ folder in the path: keep the previous convention, the sidecar directory's parent.
        return Path.GetFullPath(Path.Combine(directory, ".."));
    }

    private static string ResolvePath(string sidecarPath, string hash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sidecarPath);
        return PathForProject(ProjectRootFor(sidecarPath), hash);
    }

    private static string ValidateHash(string hash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);

        if (!hash.StartsWith(HashPrefix, StringComparison.Ordinal))
            throw new InvalidDataException(
                $"Artifact hash '{hash}' must use the '{HashPrefix}' prefix."
            );

        string hex = hash[HashPrefix.Length..];
        if (hex.Length != 64 || !IsLowerHex(hex))
            throw new InvalidDataException($"Artifact hash '{hash}' is not a SHA-256 hex digest.");

        return hex;
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
