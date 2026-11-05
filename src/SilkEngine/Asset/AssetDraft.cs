using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Asset;

/// <summary>Decoded asset content produced by a loader and registered by the engine.</summary>
/// <remarks>Loaders only decode: the engine registers <see cref="Payload"/> in the asset table and binds
/// <see cref="IAsset.AssetId"/> afterwards, so decoding can run off the asset thread.</remarks>
public sealed class AssetDraft
{
    public required IAsset Asset { get; init; }

    public required ReadOnlyMemory<byte> Payload { get; init; }
}
