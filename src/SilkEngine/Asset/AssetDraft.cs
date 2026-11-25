using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Asset;

/// <summary>Decoded asset content produced by a loader and registered by the engine.</summary>
/// <remarks>Created by a loader on its decode thread and owned by the engine until registration. Ownership of
/// <see cref="Payload"/> transfers to the store only through a successful registration; every other path
/// (duplicate path, cancel, failure) must dispose the draft. Disposing after a successful transfer is a no-op,
/// and <see cref="Payload"/> throws once the payload was transferred.</remarks>
internal sealed class AssetDraft : IDisposable
{
    private IAssetPayload? _payload;

    internal AssetDraft(IAssetPayload payload)
    {
        _payload = payload;
    }

    /// <summary>Asset factory that binds the store-issued handle.</summary>
    public required Func<AssetHandle, IAsset> AssetFactory { get; init; }

    /// <summary>Decoded payload; transferred to the store by <see cref="TakePayload"/>.</summary>
    public IAssetPayload Payload =>
        _payload
        ?? throw new InvalidOperationException("The draft payload was already transferred.");

    /// <summary>Transfers payload ownership; subsequent <see cref="Dispose"/> calls are no-ops.</summary>
    internal IAssetPayload TakePayload() =>
        Interlocked.Exchange(ref _payload, null)
        ?? throw new InvalidOperationException("The draft payload was already transferred.");

    public void Dispose() => Interlocked.Exchange(ref _payload, null)?.Dispose();
}
