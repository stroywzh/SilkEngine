using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Asset;

/// <summary>Published runtime record: the asset and its one-shot payload lease, visible together.</summary>
internal readonly record struct AssetView(AssetId Id, IAsset Asset, PayloadLease Payload);
