using System.Diagnostics.CodeAnalysis;
using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Core.AssetSystem;

/// <summary>The render side's single entry point (adapter) for consuming loaded assets.</summary>
/// <remarks>Ready/released notifications, borrowed asset reads, one-shot payload transfer and render references
/// all pass through here; render-side code never touches the store or dispatcher directly.</remarks>
internal static class AssetRenderBridge
{
    // Render thread: takes one ready id; the store published the asset before queueing it.
    internal static bool TryTakeReady(out AssetId assetId) =>
        AssetRuntime.Dispatcher.TryTakeReady(out assetId);

    // Render thread: takes one id whose record left the snapshot; no reference remains for it.
    internal static bool TryTakeReleased(out AssetId assetId) =>
        AssetRuntime.Dispatcher.TryTakeReleased(out assetId);

    // Render thread: takes one id whose store retention was released while render references remain.
    internal static bool TryTakePinReleased(out AssetId assetId) =>
        AssetRuntime.Dispatcher.TryTakePinReleased(out assetId);

    /// <summary>Borrows the published asset for a runtime binding; no reference is acquired.</summary>
    internal static bool TryGetRenderAsset(
        AssetId assetId,
        [MaybeNullWhen(false)] out IAsset asset
    )
    {
        if (AssetRuntime.Store.TryGet(assetId, out AssetView view))
        {
            asset = view.Asset;
            return true;
        }

        asset = null!;
        return false;
    }

    /// <summary>Reads the published record; does not acquire a reference.</summary>
    internal static bool TryGetView(AssetId assetId, out AssetView view) =>
        AssetRuntime.Store.TryGet(assetId, out view);

    /// <summary>Acquires one render reference and returns the asset; the caller must release it.</summary>
    internal static bool TryAcquire<T>(AssetId assetId, [MaybeNullWhen(false)] out T asset)
        where T : IAsset
    {
        asset = default;
        if (!AssetRuntime.Store.TryGet(assetId, out AssetView view) || !view.Lifetime.TryAcquire())
            return false;

        if (view.Asset is T typed)
        {
            asset = typed;
            return true;
        }

        // The reference is useless without the requested type; hand it back.
        view.Lifetime.ReleaseRenderRef();
        return false;
    }

    /// <summary>Takes the decoded payload once; the caller owns it afterwards.</summary>
    internal static bool TryTakePayload<T>(AssetId assetId, [MaybeNullWhen(false)] out T payload)
        where T : class, IAssetPayload
    {
        payload = null!;
        return AssetRuntime.Store.TryGet(assetId, out AssetView view)
            && view.Payload.TryTake<T>(out payload);
    }

    /// <summary>Reads the payload without taking ownership.</summary>
    internal static bool TryPeekPayload<T>(AssetId assetId, [MaybeNullWhen(false)] out T payload)
        where T : class, IAssetPayload
    {
        payload = null!;
        return AssetRuntime.Store.TryGet(assetId, out AssetView view)
            && view.Payload.TryPeek<T>(out payload);
    }

    /// <summary>True while the store retention is held; a queued pin release is stale then and keeps the binding.</summary>
    internal static bool IsPinHeld(AssetId assetId) =>
        TryGetView(assetId, out AssetView view) && view.Lifetime.RetentionHeld;

    /// <summary>Releases one render reference; off the owner thread the release is queued and applied later.</summary>
    internal static bool Release(AssetId assetId)
    {
        if (AssetRuntime.Dispatcher.IsOwnerThread)
        {
            bool released = AssetRuntime.Store.ReleaseRenderRef(assetId);

            // The synchronous path must not leave the removal invisible to readers.
            AssetRuntime.Store.Commit();
            return released;
        }

        if (!AssetRuntime.Dispatcher.HasOwner)
        {
            Log.Warning(
                $"Asset {assetId.Value} release ignored: the asset pipeline is not running."
            );
            return false;
        }

        AssetRuntime.Dispatcher.Submit(AssetCommand.ReleaseRenderRef(assetId));
        return true;
    }

    /// <summary>Queued requests plus requests currently decoding on the thread pool.</summary>
    internal static int PendingCount => AssetRuntime.Dispatcher.PendingCount;

    /// <summary>Assets inside their release window plus released ids the render side has not taken yet.</summary>
    internal static int PendingReleaseCount =>
        AssetRuntime.Store.DeferredCount + AssetRuntime.Dispatcher.Released.Count;
}
