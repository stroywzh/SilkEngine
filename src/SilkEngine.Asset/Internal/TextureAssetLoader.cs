using System.IO;
using SilkEngine.Asset.Decoder.Png;
using SilkEngine.Asset.Sidecar;
using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Asset.Internal;

/// <summary>Runtime texture loader.</summary>
internal sealed class TextureAssetLoader : IAssetLoader
{
    // Thread-pool thread only: decodes and returns a draft; the engine registers and binds it.
    public AssetDraft Load(string sourcePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        string fullSourcePath = Path.GetFullPath(sourcePath);
        string sidecarPath = SidecarPath.ForSource(fullSourcePath);
        if (!File.Exists(sidecarPath))
        {
            throw new AssetNotImportedException(
                $"Texture sidecar not found for '{sourcePath}'. Run the importer first (expected '{sidecarPath}')."
            );
        }

        // 反序列化 + 基础契约校验（schemaVersion / assetType / source）在 Reader 内完成。
        TextureSidecar sidecar = TextureSidecar.Reader.Read(sidecarPath);

        string decodedSourcePath = SidecarPath.ResolveSource(sidecarPath, sidecar.Source);
        if (!File.Exists(decodedSourcePath))
        {
            throw new FileNotFoundException(
                $"Texture source not found: '{decodedSourcePath}'.",
                decodedSourcePath
            );
        }

        byte[] bytes = File.ReadAllBytes(decodedSourcePath);
        string sourceHash = ContentHash.Sha256(bytes);
        if (!string.Equals(sourceHash, sidecar.SourceHash, StringComparison.OrdinalIgnoreCase))
        {
            // 源文件变了但 sidecar 没重导入：只警告，重写是 importer 的职责。
            Log.Warning(
                $"Texture sidecar '{sidecarPath}' is stale: sourceHash does not match '{decodedSourcePath}'."
            );
        }

        PngDecodedImage decoded;
        using (var stream = new MemoryStream(bytes, writable: false))
        {
            decoded = PngDecoder.Decode(stream);
        }

        if (decoded.Width != sidecar.Width || decoded.Height != sidecar.Height)
        {
            Log.Warning(
                $"Texture sidecar '{sidecarPath}' records {sidecar.Width}x{sidecar.Height} but the source decodes to {decoded.Width}x{decoded.Height}."
            );
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
