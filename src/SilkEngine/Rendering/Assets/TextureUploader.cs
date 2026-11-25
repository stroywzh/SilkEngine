using SilkEngine.Asset.Payload;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.Assets;

/// <summary>Uploads a decoded RGBA32 texture and consumes its CPU payload; runs on the render thread.</summary>
internal sealed class TextureUploader : IAssetUploader
{
    /// <inheritdoc />
    public Type AssetType => typeof(Texture);

    /// <inheritdoc />
    public AssetUploadResult TryUpload(in AssetUploadContext context, out IRuntimeBinding binding)
    {
        binding = null!;

        // Peek the payload and create the device texture before taking ownership of the data.
        if (!context.TryGetAsset<Texture>(out Texture texture))
            return AssetUploadResult.Failed;

        if (!context.TryPeekPayload<TexturePayload>(out TexturePayload payload))
            return AssetUploadResult.Failed;

        TextureHandle handle;
        try
        {
            handle = context.Device.CreateTexture(
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
        catch (Exception error) when (error is ArgumentException or NotSupportedException)
        {
            // Unusable data for this device; the table records a failed upload and keeps the fallback.
            Log.Exception(error);
            return AssetUploadResult.Failed;
        }

        // Transfer ownership only after the device accepted the data.
        if (!context.TryTakePayload<TexturePayload>(out TexturePayload owned))
        {
            context.Device.DestroyTexture(handle);
            return AssetUploadResult.Failed;
        }

        owned.Dispose();

        binding = new TextureBinding { Texture = texture, Handle = handle };
        return AssetUploadResult.Uploaded;
    }
}
