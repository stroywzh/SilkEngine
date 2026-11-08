using System.IO;

namespace SilkEngine.Core.IO;

/// <summary>Atomic file writes: materialize a temp file beside the target, then rename.</summary>
internal static class AtomicFile
{
    /// <summary>Writes <paramref name="content"/> to <paramref name="path"/> via a same-directory temp file.</summary>
    internal static void WriteAllBytes(string path, ReadOnlySpan<byte> content, bool overwrite)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string target = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        // Same-directory temp file so the rename stays atomic on one volume.
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

            File.Move(temp, target, overwrite);
        }
        finally
        {
            TryDelete(temp);
        }
    }

    /// <summary>Writes only when the target does not exist yet; an existing target wins (dedup).</summary>
    internal static bool TryWriteNew(string path, ReadOnlySpan<byte> content)
    {
        string target = Path.GetFullPath(path);
        if (File.Exists(target))
            return false;

        try
        {
            WriteAllBytes(target, content, overwrite: false);
            return true;
        }
        catch (IOException) when (File.Exists(target))
        {
            // Another writer materialized the same content first: the target wins.
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
    }
}
