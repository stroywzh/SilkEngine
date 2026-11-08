using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Asset;

/// <summary>Published runtime record: the asset, its one-shot payload lease and its lifetime.</summary>
internal readonly record struct AssetView(
    AssetId Id,
    IAsset Asset,
    PayloadLease Payload,
    AssetLifetime Lifetime
);
