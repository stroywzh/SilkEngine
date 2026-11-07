using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Asset;

/// <summary>Decoded asset content produced by a loader and registered by the engine.</summary>
/// <remarks>Loaders only decode: the engine registers <see cref="Payload"/> with the asset store and binds
/// <see cref="IAsset.AssetId"/> afterwards, so decoding can run off the asset thread.</remarks>
internal sealed class AssetDraft
{
    /// <summary>绑定 AssetId 的资产工厂；绑定后为轻量元数据实例。</summary>
    public required Func<AssetId, IAsset> AssetFactory { get; init; }

    /// <summary>已解码载荷；具体类型由加载器决定。</summary>
    public required IAssetPayload Payload { get; init; }
}
