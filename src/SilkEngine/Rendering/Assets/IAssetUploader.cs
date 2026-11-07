using System.Diagnostics.CodeAnalysis;
using SilkEngine.Asset;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.Assets;

/// <summary>Turns one ready asset id into a device binding; called on the render thread.</summary>
/// <remarks>The asset pipeline stays behind <see cref="SilkEngine.Assets"/>: an uploader receives the id and
/// asks the internal bridge for the asset/payload it needs.</remarks>
public interface IAssetUploader
{
    /// <summary>Asset type this uploader handles.</summary>
    Type AssetType { get; }

    /// <summary>Uploads the asset's payload and returns the binding that owns the device resource.</summary>
    /// <remarks>Called on the render thread; returning false leaves the asset pending for a later retry.</remarks>
    bool TryUpload(
        IRenderDevice device,
        AssetId assetId,
        [MaybeNullWhen(false)] out IRuntimeBinding binding
    );
}
