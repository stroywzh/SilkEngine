using System.IO;
using SilkEngine.Asset;
using SilkEngine.Core.AssetSystem.Internal.Png;

namespace SilkEngine.Core.AssetSystem.Internal;

/// <summary>Runtime texture loader.</summary>
internal sealed class TextureAssetLoader : IAssetLoader
{
    // Thread-pool thread only: decodes and returns a draft; the engine registers and binds it.
    public AssetDraft Load(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        string fullSourcePath = Path.GetFullPath(sourcePath);
        string sidecarPath = TextureSidecar.PathFor(fullSourcePath);
        if (!File.Exists(sidecarPath))
        {
            throw new AssetNotImportedException(
                $"Texture sidecar not found for '{sourcePath}'. Run the importer first (expected '{sidecarPath}').");
        }

        // The sidecar is the disk contract: schemaVersion / assetType / source validation happens here.
        TextureSidecar sidecar = TextureSidecar.Load(sidecarPath);

        string decodedSourcePath = sidecar.ResolveSourcePath(sidecarPath);
        if (!File.Exists(decodedSourcePath))
        {
            throw new FileNotFoundException(
                $"Texture source not found: '{decodedSourcePath}'.",
                decodedSourcePath);
        }

        byte[] bytes = File.ReadAllBytes(decodedSourcePath);
        string sourceHash = TextureSidecar.ComputeSourceHash(bytes);
        if (!string.Equals(sourceHash, sidecar.SourceHash, StringComparison.OrdinalIgnoreCase))
        {
            // A source-hash mismatch only warns; rewriting the sidecar is the importer's job.
            Log.Warning(
                $"Texture sidecar '{sidecarPath}' is stale: sourceHash does not match '{decodedSourcePath}'.");
        }

        PngDecodedImage decoded;
        using (var stream = new MemoryStream(bytes, writable: false))
        {
            decoded = PngDecoder.Decode(stream);
        }

        if (decoded.Width != sidecar.Width || decoded.Height != sidecar.Height)
        {
            Log.Warning(
                $"Texture sidecar '{sidecarPath}' records {sidecar.Width}x{sidecar.Height} but the source decodes to {decoded.Width}x{decoded.Height}.");
        }

        var texture = new Texture(AssetId.Invalid)
        {
            Width = decoded.Width,
            Height = decoded.Height,
            Format = sidecar.Format,
            ColorSpace = sidecar.ColorSpace,
            MipCount = sidecar.MipCount,
        };

        return new AssetDraft { Asset = texture, Payload = decoded.Rgba32 };
    }
}
