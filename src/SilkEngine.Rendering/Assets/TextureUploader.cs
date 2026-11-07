using System.Diagnostics.CodeAnalysis;
using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;
using SilkEngine.Asset.Payload;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.Assets;

/// <summary>Uploads a decoded RGBA32 texture and consumes its CPU payload; runs on the render thread.</summary>
/// <remarks>The asset pipeline itself stays internal: the uploader receives the <see cref="AssetId"/> and asks
/// the asset bridge for the descriptor and the one-shot payload.</remarks>
public sealed class TextureUploader : IAssetUploader
{
    /// <inheritdoc />
    public Type AssetType => typeof(Texture);

    /// <inheritdoc />
    public bool TryUpload(
        IRenderDevice device,
        AssetId assetId,
        [MaybeNullWhen(false)] out IRuntimeBinding binding
    )
    {
        binding = null;

        if (
            !SilkEngine.Assets.TryGetRenderAsset(assetId, out IAsset? asset)
            || asset is not Texture texture
        )
            return false;

        if (!SilkEngine.Assets.TryTakePayload<TexturePayload>(assetId, out TexturePayload? payload))
            return false;

        TextureHandle handle;
        try
        {
            handle = device.CreateTexture(
                new TextureDesc(
                    texture.Width,
                    texture.Height,
                    texture.Format,
                    texture.ColorSpace,
                    texture.MipCount,
                    payload.Rgba32
                )
            );
        }
        catch
        {
            // The upload failed: release the CPU copy now instead of waiting for the asset release.
            payload.Dispose();
            throw;
        }

        // The upload consumed the CPU copy; the payload lease is now empty.
        payload.Dispose();

        binding = new TextureBinding { Texture = texture, Handle = handle };

        if (Application.LogSetting.EnableRenderLog)
        {
            Log.Debug(
                $"Uploaded texture {texture.Width}x{texture.Height} for asset {texture.AssetId.Value}."
            );
        }

        return true;
    }
}
