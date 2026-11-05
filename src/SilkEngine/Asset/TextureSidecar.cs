using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SilkEngine.Asset;

/// <summary>Texture import settings (the sidecar's <c>importSettings</c> node).</summary>
public sealed record TextureImportSettings
{
    /// <summary>Whether colors are interpreted as sRGB (default true).</summary>
    public bool Srgb { get; init; } = true;
}

/// <summary>Sidecar stored next to the source file (JSON schema v1).</summary>
public sealed class TextureSidecar
{
    /// <summary>Current schema version.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Asset type identifier.</summary>
    public const string TextureAssetType = "texture";

    /// <summary>Importer name.</summary>
    public const string PngImporterName = "png";

    /// <summary>Current importer version.</summary>
    public const int CurrentImporterVersion = 1;

    /// <summary>Sidecar extension.</summary>
    public const string SidecarExtension = ".asset";

    private const string HashPrefix = "sha256:";

    private static readonly JsonSerializerOptions s_options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Sidecar schema version.</summary>
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>Asset type.</summary>
    public string AssetType { get; init; } = TextureAssetType;

    /// <summary>Importer name.</summary>
    public string Importer { get; init; } = PngImporterName;

    /// <summary>Importer version.</summary>
    public int ImporterVersion { get; init; } = CurrentImporterVersion;

    /// <summary>Source file name, relative to the sidecar directory.</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Source content hash (<c>sha256:...</c>).</summary>
    public string SourceHash { get; init; } = string.Empty;

    /// <summary>Texture width in pixels.</summary>
    public int Width { get; init; }

    /// <summary>Texture height in pixels.</summary>
    public int Height { get; init; }

    /// <summary>Pixel format.</summary>
    public TextureFormat Format { get; init; } = TextureFormat.RGBA32;

    /// <summary>Color space.</summary>
    public TextureColorSpace ColorSpace { get; init; } = TextureColorSpace.SRGB;

    /// <summary>Mip count.</summary>
    public int MipCount { get; init; } = 1;

    /// <summary>Import settings.</summary>
    public TextureImportSettings ImportSettings { get; init; } = new();

    /// <summary>Derives the sidecar path from the source path.</summary>
    /// <returns>Sidecar path next to the source file.</returns>
    public static string PathFor(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        return sourcePath + SidecarExtension;
    }

    /// <summary>Computes the source content hash.</summary>
    /// <returns>Hash string (<c>sha256:&lt;64 lowercase hex&gt;</c>).</returns>
    public static string ComputeSourceHash(ReadOnlySpan<byte> content) =>
        HashPrefix + Convert.ToHexStringLower(SHA256.HashData(content));

    /// <summary>Reads the source file and computes its hash.</summary>
    /// <returns>Hash string (<c>sha256:&lt;64 lowercase hex&gt;</c>).</returns>
    public static string ComputeSourceHash(string sourcePath) =>
        ComputeSourceHash(File.ReadAllBytes(sourcePath));

    /// <summary>Whether this sidecar is an up-to-date import result.</summary>
    /// <returns>True when no rewrite is needed.</returns>
    public bool IsCurrentFor(string sourceName, string sourceHash, TextureImportSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return SchemaVersion == CurrentSchemaVersion
            && string.Equals(AssetType, TextureAssetType, StringComparison.Ordinal)
            && string.Equals(Importer, PngImporterName, StringComparison.Ordinal)
            && ImporterVersion == CurrentImporterVersion
            && string.Equals(Source, sourceName, StringComparison.Ordinal)
            && string.Equals(SourceHash, sourceHash, StringComparison.OrdinalIgnoreCase)
            && ImportSettings is not null
            && ImportSettings == settings
            && Format == TextureFormat.RGBA32
            && MipCount == 1
            && Width > 0
            && Height > 0;
    }

    /// <summary>Serializes to JSON.</summary>
    /// <returns>JSON text.</returns>
    public string ToJson() => JsonSerializer.Serialize(this, s_options);

    /// <summary>Writes the sidecar atomically.</summary>
    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string target = Path.GetFullPath(path);
        string? dir = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        string temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, ToJson(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temp, target, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException($"Cannot write texture sidecar '{path}': {ex.Message}", ex);
        }
        finally
        {
            TryDelete(temp);
        }
    }

    /// <summary>Reads and validates the sidecar.</summary>
    /// <returns>Deserialized sidecar.</returns>
    public static TextureSidecar Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string json;
        try
        {
            json = File.ReadAllText(path);
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
            throw new IOException($"Cannot read texture sidecar '{path}': {ex.Message}", ex);
        }

        TextureSidecar? sidecar;
        try
        {
            sidecar = JsonSerializer.Deserialize<TextureSidecar>(json, s_options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Malformed texture sidecar '{path}': {ex.Message}", ex);
        }

        if (sidecar is null)
            throw new InvalidDataException($"Empty texture sidecar '{path}'.");
        if (sidecar.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidDataException(
                $"Unsupported schemaVersion {sidecar.SchemaVersion} in '{path}' (expected {CurrentSchemaVersion}).");
        if (!string.Equals(sidecar.AssetType, TextureAssetType, StringComparison.Ordinal))
            throw new InvalidDataException(
                $"Unexpected assetType '{sidecar.AssetType}' in '{path}' (expected '{TextureAssetType}').");
        if (string.IsNullOrWhiteSpace(sidecar.Source))
            throw new InvalidDataException($"Missing source in texture sidecar '{path}'.");

        return sidecar;
    }

    /// <summary>Resolves the absolute source path for this sidecar.</summary>
    /// <returns>Absolute source path.</returns>
    public string ResolveSourcePath(string sidecarPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sidecarPath);

        if (string.IsNullOrWhiteSpace(Source))
            throw new InvalidDataException($"Missing source in texture sidecar '{sidecarPath}'.");
        if (Path.IsPathRooted(Source))
            throw new InvalidDataException(
                $"source must be a relative file name, got '{Source}' in '{sidecarPath}'.");

        string dir = Path.GetDirectoryName(Path.GetFullPath(sidecarPath)) ?? string.Empty;
        string combined = Path.GetFullPath(Path.Combine(dir, Source));
        string dirWithSeparator = dir.EndsWith(Path.DirectorySeparatorChar)
            ? dir
            : dir + Path.DirectorySeparatorChar;

        if (dir.Length > 0 && !combined.StartsWith(dirWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(
                $"source '{Source}' in '{sidecarPath}' escapes the sidecar directory.");

        return combined;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
