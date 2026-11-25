using System.Diagnostics.CodeAnalysis;
using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;
using SilkEngine.AssetSystem.Runtime;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.Assets;

/// <summary>Result of one render-side asset upload attempt.</summary>
internal enum AssetUploadResult : byte
{
    /// <summary>The binding was created and the asset is ready to draw.</summary>
    Uploaded = 0,

    /// <summary>The asset data cannot be uploaded; the table records the failure and keeps the fallback.</summary>
    Failed = 1,
}

/// <summary>One upload attempt's view of the asset and the device, without exposing asset identity.</summary>
internal readonly struct AssetUploadContext
{
    private readonly IRenderAssetSource _source;
    private readonly AssetId _assetId;

    internal AssetUploadContext(IRenderAssetSource source, AssetId assetId, IRenderDevice device)
    {
        _source = source;
        _assetId = assetId;
        Device = device;
    }

    internal IRenderDevice Device { get; }

    internal bool TryGetAsset<T>([NotNullWhen(true)] out T asset)
        where T : class, IAsset
    {
        if (_source.TryGetRenderAsset(_assetId, out IAsset? published) && published is T typed)
        {
            asset = typed;
            return true;
        }

        asset = default!;
        return false;
    }

    internal bool TryPeekPayload<T>(out T payload)
        where T : class, IAssetPayload => _source.TryPeekPayload(_assetId, out payload);

    internal bool TryTakePayload<T>(out T payload)
        where T : class, IAssetPayload => _source.TryTakePayload(_assetId, out payload);
}

/// <summary>Turns one ready asset id into a device binding; called on the render thread.</summary>
/// <remarks>Engine-internal: the context supplies the descriptor and payload. Return
/// <see cref="AssetUploadResult.Failed"/> for unusable data; device contract failures must propagate.</remarks>
internal interface IAssetUploader
{
    /// <summary>Asset type this uploader handles; matched against the asset's exact CLR type.</summary>
    Type AssetType { get; }

    /// <summary>Uploads the asset's payload and returns the binding that owns the device resource.</summary>
    AssetUploadResult TryUpload(in AssetUploadContext context, out IRuntimeBinding binding);
}
