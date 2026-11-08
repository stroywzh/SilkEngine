using System.IO;
using System.Text;
using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;
using SilkEngine.Asset.Payload;
using SilkEngine.Serialization;
using SilkEngine.Serialization.DataStructure;

namespace SilkEngine.Asset.Internal;

/// <summary>Runtime texture loader.</summary>
internal sealed class TextureAssetLoader : IAssetLoader
{
    // Thread-pool thread only: reads the catalog header and the artifact, then returns a draft;
    // the engine registers and binds it.
    public AssetDraft Load(in AssetSource source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source.ProjectPath);

        if (!string.Equals(source.AssetType, AssetConstants.TextureAssetName, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Asset '{source.ProjectPath}' is '{source.AssetType}', not '{AssetConstants.TextureAssetName}'."
            );
        }

        // The header comes from the catalog; a missing one means the catalog predates the artifact
        // model. Reading the sidecar or decoding the source here would put Content back on the
        // runtime path, so the asset must be reimported instead.
        if (string.IsNullOrEmpty(source.HeaderJson))
        {
            throw new InvalidDataException(
                $"Texture header missing for '{source.ProjectPath}'. Reimport the asset and rebuild the catalog."
            );
        }

        // The loader only reads the content-addressed artifact: no PNG decode and no sidecar read.
        if (string.IsNullOrEmpty(source.ArtifactPath) || !File.Exists(source.ArtifactPath))
        {
            throw new AssetNotImportedException(
                $"Texture artifact missing for '{source.ProjectPath}'. Reimport the asset and rebuild the catalog."
            );
        }

        // The header document deserializes into the content type the pipeline consumes; the draft factory
        // later binds it to the store-issued handle.
        TextureData data = JsonCodec.Decode<TextureData>(
            Encoding.UTF8.GetBytes(source.HeaderJson),
            source.SidecarPath
        );

        byte[] pixels = File.ReadAllBytes(source.ArtifactPath);

        // 载荷必须与 header 描述一致：宁可失败也不上传尺寸不符的像素。
        long expected = (long)data.Width * data.Height * 4;
        if (pixels.Length != expected)
        {
            throw new InvalidDataException(
                $"Texture payload for '{source.ProjectPath}' has {pixels.Length} bytes, "
                    + $"expected {expected} (RGBA32 {data.Width}x{data.Height})."
            );
        }

        var payload = new TexturePayload { Rgba32 = pixels };

        return new AssetDraft
        {
            AssetFactory = handle => new Texture(handle, data),
            Payload = payload,
        };
    }
}
