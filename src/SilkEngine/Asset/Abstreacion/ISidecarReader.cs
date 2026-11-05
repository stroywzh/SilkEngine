using System.Diagnostics.CodeAnalysis;

namespace SilkEngine.Asset.Abstractions;

public interface ISidecarReader<out T>
    where T : AssetSidecarBase
{
    T Read(string sidecarPath);
}

public interface ISidecarWriter<in T>
    where T : AssetSidecarBase
{
    void Write(string sidecarPath, T sidecar);
}

public sealed class SidecarFileStore<T> : ISidecarReader<T>, ISidecarWriter<T>
    where T : AssetSidecarBase
{
    private readonly ISidecarCodec<T> _codec;
    private readonly Action<T, string> _validate;

    public SidecarFileStore(ISidecarCodec<T> codec, Action<T, string>? validate = null)
    {
        _codec = codec;
        _validate = validate ?? (static (_, _) => { });
    }

    public T Read(string sidecarPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sidecarPath);

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(sidecarPath);
        }
        catch (FileNotFoundException)
        {
            throw;
        }
        catch (DirectoryNotFoundException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"Cannot read sidecar '{sidecarPath}': {ex.Message}", ex);
        }

        T sidecar = _codec.Decode(bytes, sidecarPath);
        _validate(sidecar, sidecarPath);
        return sidecar;
    }

    public bool TryRead(string sidecarPath, [MaybeNullWhen(false)] out T sidecar)
    {
        try
        {
            if (!File.Exists(sidecarPath))
            {
                sidecar = null;
                return false;
            }

            sidecar = Read(sidecarPath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            sidecar = null;
            return false;
        }
    }

    public void Write(string sidecarPath, T sidecar)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sidecarPath);
        ArgumentNullException.ThrowIfNull(sidecar);

        string target = Path.GetFullPath(sidecarPath);
        string? dir = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        string temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(temp, _codec.Encode(sidecar));
            File.Move(temp, target, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"Cannot write sidecar '{sidecarPath}': {ex.Message}", ex);
        }
        finally
        {
            try
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
