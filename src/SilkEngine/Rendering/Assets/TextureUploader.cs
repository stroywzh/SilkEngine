using System.Diagnostics.CodeAnalysis;
using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;
using SilkEngine.Asset.Payload;
using SilkEngine.Core.AssetSystem;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.Assets;

/// <summary>Uploads a decoded RGBA32 texture and consumes its CPU payload; runs on the render thread.</summary>
internal sealed class TextureUploader : IAssetUploader
{
    /// <inheritdoc />
    public Type AssetType => typeof(Texture);

    /// <inheritdoc />
    public AssetUploadResult TryUpload(
        IRenderDevice device,
        AssetId assetId,
        [MaybeNullWhen(false)] out IRuntimeBinding binding
    )
    {
        binding = null!;
        
        //先Peek和Create，最后转移所有权
        if (
            !AssetRenderBridge.TryGetRenderAsset(assetId, out IAsset? asset)
            || asset is not Texture texture
        )
            return AssetUploadResult.PermanentFailure;

        if (!AssetRenderBridge.TryPeekPayload<TexturePayload>(assetId, out TexturePayload? payload))
            return AssetUploadResult.PermanentFailure;

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
        catch (Exception error) when (error is ArgumentException or NotSupportedException)
        {
            Log.Exception(error);
            return AssetUploadResult.PermanentFailure;
        }
        catch (Exception error)
        {
            Log.Exception(error);
            return AssetUploadResult.RetryableFailure;
        }

        // 成功后才消费 payload
        if (!AssetRenderBridge.TryTakePayload<TexturePayload>(assetId, out TexturePayload? owned))
        {
            device.DestroyTexture(handle);
            return AssetUploadResult.RetryableFailure;
        }

        owned.Dispose();

        binding = new TextureBinding { Texture = texture, Handle = handle };
        return AssetUploadResult.Uploaded;
    }
}
