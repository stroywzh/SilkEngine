using System.Diagnostics.CodeAnalysis;
using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;

namespace SilkEngine.AssetSystem.Runtime;

/// <summary>The render side's single entry point (adapter) for consuming loaded assets.</summary>
/// <remarks>Ready/released notifications, borrowed asset reads, one-shot payload transfer and render references
/// all pass through here; render-side code never touches the store or dispatcher directly. The render table
/// receives this through <see cref="IRenderAssetSource"/> so tests can substitute a fake.</remarks>
internal sealed class AssetRenderBridge : IRenderAssetSource
{
    /// <summary>Process-wide bridge used by the engine runtime table.</summary>
    internal static AssetRenderBridge Instance { get; } = new();

    private AssetRenderBridge() { }

    /// <inheritdoc />
    public bool TryTakeReady(out AssetId assetId) =>
        AssetSystemHost.Dispatcher.TryTakeReady(out assetId);

    /// <inheritdoc />
    public bool TryTakeReleased(out AssetId assetId) =>
        AssetSystemHost.Dispatcher.TryTakeReleased(out assetId);

    /// <inheritdoc />
    public bool TryTakePinReleased(out AssetId assetId) =>
        AssetSystemHost.Dispatcher.TryTakePinReleased(out assetId);

    /// <inheritdoc />
    public bool TryGetRenderAsset(AssetId assetId, [MaybeNullWhen(false)] out IAsset asset)
    {
        if (AssetSystemHost.Store.TryGet(assetId, out AssetView view))
        {
            asset = view.Asset;
            return true;
        }

        asset = null!;
        return false;
    }

    /// <inheritdoc />
    public bool TryGetView(AssetId assetId, out AssetView view) =>
        AssetSystemHost.Store.TryGet(assetId, out view);

    /// <inheritdoc />
    public bool TryAcquire<T>(AssetId assetId, [MaybeNullWhen(false)] out T asset)
        where T : IAsset
    {
        asset = default;
        if (
            !AssetSystemHost.Store.TryGet(assetId, out AssetView view)
            || !view.Lifetime.AddRenderRefCount()
        )
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

    /// <inheritdoc />
    public bool TryTakePayload<T>(AssetId assetId, out T payload)
        where T : class, IAssetPayload
    {
        payload = null!;
        return AssetSystemHost.Store.TryGet(assetId, out AssetView view)
            && view.Payload.TryTake<T>(out payload);
    }

    /// <inheritdoc />
    public bool TryPeekPayload<T>(AssetId assetId, out T payload)
        where T : class, IAssetPayload
    {
        payload = null!;
        return AssetSystemHost.Store.TryGet(assetId, out AssetView view)
            && view.Payload.TryPeek<T>(out payload);
    }

    /// <inheritdoc />
    public bool IsRetentionCount(AssetId assetId) =>
        TryGetView(assetId, out AssetView view) && view.Lifetime.RetentionCount;

    /// <inheritdoc />
    public bool Release(AssetId assetId)
    {
        if (AssetSystemHost.Dispatcher.IsOwnerThread)
        {
            bool released = AssetSystemHost.Store.ReleaseRenderRef(assetId);

            // The synchronous path must not leave the removal invisible to readers.
            AssetSystemHost.Store.Commit();
            return released;
        }

        // Off the owner thread the release is queued; a stopped pipeline can never apply it.
        if (!AssetSystemHost.Dispatcher.HasOwner)
        {
            Log.Error($"Asset {assetId.Value} release ignored: the asset pipeline is not running.");
            return false;
        }

        AssetSystemHost.Dispatcher.Submit(AssetCommand.ReleaseRenderRef(assetId));
        return true;
    }

    /// <inheritdoc />
    public int PendingCount => AssetSystemHost.Dispatcher.PendingCount;

    /// <inheritdoc />
    public int PendingReleaseCount =>
        AssetSystemHost.Store.DeferredCount + AssetSystemHost.Dispatcher.Ready.Count;
}
