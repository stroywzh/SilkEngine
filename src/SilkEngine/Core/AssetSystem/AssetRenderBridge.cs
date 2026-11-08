using System.Diagnostics.CodeAnalysis;
using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Core.AssetSystem;

/// <summary>Engine-internal bridge used by the render side to consume loaded assets.</summary>
/// <remarks>This is deliberately not part of the public <see cref="SilkEngine.Assets"/> facade: ready/released
/// notifications, borrowed asset reads, one-shot payload transfer and render references stay inside the engine.</remarks>
internal static class AssetRenderBridge
{
    internal static bool TryTakeReady(out AssetId assetId) => AssetRuntime.TryTakeReady(out assetId);

    internal static bool TryTakeReleased(out AssetId assetId) =>
        AssetRuntime.TryTakeReleased(out assetId);

    internal static bool TryTakePinReleased(out AssetId assetId) =>
        AssetRuntime.TryTakePinReleased(out assetId);

    internal static bool TryGetRenderAsset(
        AssetId assetId,
        [MaybeNullWhen(false)] out IAsset asset
    ) => AssetRuntime.TryGetRenderAsset(assetId, out asset);

    internal static bool TryAcquire<T>(AssetId assetId, [MaybeNullWhen(false)] out T asset)
        where T : IAsset => AssetRuntime.TryAcquire(assetId, out asset);

    internal static bool TryTakePayload<T>(AssetId assetId, [MaybeNullWhen(false)] out T payload)
        where T : class, IAssetPayload => AssetRuntime.TryTakePayload(assetId, out payload);

    internal static bool TryPeekPayload<T>(AssetId assetId, [MaybeNullWhen(false)] out T payload)
        where T : class, IAssetPayload => AssetRuntime.TryPeekPayload(assetId, out payload);

    internal static bool IsPinHeld(AssetId assetId) =>
        AssetRuntime.TryGetView(assetId, out AssetView view) && view.Lifetime.PinHeld;

    internal static bool Release(AssetId assetId) => AssetRuntime.Release(assetId);

    internal static int PendingCount => AssetRuntime.PendingCount;

    internal static int PendingReleaseCount => AssetRuntime.PendingReleaseCount;
}
