namespace SilkEngine.Asset;

/// <summary>Decoded asset content produced by a loader and registered by the engine.</summary>
/// <remarks>Loaders only decode: the engine registers <see cref="Payload"/> in the asset table and binds
/// <see cref="IAsset.AssetId"/> afterwards, so decoding can run off the asset thread.</remarks>
public sealed class AssetDraft
{
    /// <summary>Unbound asset; its <see cref="IAsset.AssetId"/> is <see cref="AssetId.Invalid"/> until the engine binds it.</summary>
    public required IAsset Asset { get; init; }

    /// <summary>CPU payload registered under the asset id (texture: RGBA32 pixels).</summary>
    public required ReadOnlyMemory<byte> Payload { get; init; }
}
