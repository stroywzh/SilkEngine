using System.Diagnostics.CodeAnalysis;
using SilkEngine.Asset;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.Assets;

/// <summary>Result of one render-side asset upload attempt.</summary>
internal enum AssetUploadResult : byte
{
    /// <summary>The binding was created and the asset is ready to draw.</summary>
    Uploaded = 0,

    /// <summary>The payload is not available yet; retry with a bounded attempt count.</summary>
    RetryableFailure = 1,

    /// <summary>The asset cannot be uploaded (wrong type, payload consumed, device rejected it).</summary>
    PermanentFailure = 2,
}

/// <summary>Turns one ready asset id into a device binding; called on the render thread.</summary>
/// <remarks>Engine-internal: uploaders use <c>AssetRenderBridge</c> for the descriptor/payload. Implementations
/// must not throw into the render loop; report <see cref="AssetUploadResult.PermanentFailure"/> instead.</remarks>
internal interface IAssetUploader
{
    /// <summary>Asset type this uploader handles.</summary>
    Type AssetType { get; }

    /// <summary>Uploads the asset's payload and returns the binding that owns the device resource.</summary>
    AssetUploadResult TryUpload(
        IRenderDevice device,
        AssetId assetId,
        [MaybeNullWhen(false)] out IRuntimeBinding binding
    );
}
