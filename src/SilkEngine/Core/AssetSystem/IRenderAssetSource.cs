using System.Diagnostics.CodeAnalysis;
using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Core.AssetSystem;

/// <summary>Render-side consumption of the asset pipeline; the runtime table depends on this seam, not on the bridge.</summary>
/// <remarks>Asset identity never leaves this boundary: bindings and pipelines receive render handles instead.
/// The engine implementation is <see cref="Runtime.AssetRenderBridge.Instance"/>; tests and tools inject a fake.</remarks>
internal interface IRenderAssetSource
{
    /// <summary>Takes one id whose record became ready; the store published the asset before queueing it.</summary>
    bool TryTakeReady(out AssetId assetId);

    /// <summary>Takes one id whose record left the snapshot; no reference remains for it.</summary>
    bool TryTakeReleased(out AssetId assetId);

    /// <summary>Takes one id whose store retention was released while render references remain.</summary>
    bool TryTakePinReleased(out AssetId assetId);

    /// <summary>Borrows the published asset for a runtime binding; no reference is acquired.</summary>
    bool TryGetRenderAsset(AssetId assetId, [MaybeNullWhen(false)] out IAsset asset);

    /// <summary>Reads the published record; does not acquire a reference.</summary>
    bool TryGetView(AssetId assetId, out AssetView view);

    /// <summary>Acquires one render reference and returns the asset; the caller must release it.</summary>
    bool TryAcquire<T>(AssetId assetId, [MaybeNullWhen(false)] out T asset)
        where T : IAsset;

    /// <summary>Takes the decoded payload once; the caller owns it afterwards.</summary>
    bool TryTakePayload<T>(AssetId assetId, out T payload)
        where T : class, IAssetPayload;

    /// <summary>Reads the payload without taking ownership.</summary>
    bool TryPeekPayload<T>(AssetId assetId, out T payload)
        where T : class, IAssetPayload;

    /// <summary>True while the store retention is held; a queued retention release is stale then.</summary>
    bool IsRetentionCount(AssetId assetId);

    /// <summary>Releases one render reference; off the owner thread the release is queued and applied later.</summary>
    bool Release(AssetId assetId);

    /// <summary>Queued requests plus requests currently decoding on the thread pool.</summary>
    int PendingCount { get; }

    /// <summary>Assets inside their release window plus released ids the render side has not taken yet.</summary>
    int PendingReleaseCount { get; }
}
