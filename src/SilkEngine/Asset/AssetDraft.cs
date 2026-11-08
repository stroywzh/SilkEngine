using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Asset;

/// <summary>Decoded asset content produced by a loader and registered by the engine.</summary>
/// <remarks>Ownership of <see cref="Payload"/> transfers to the store only after a successful registration;
/// every other path (duplicate path, cancel, failure) must dispose the draft.</remarks>
internal sealed class AssetDraft : IDisposable
{
    private IAssetPayload? _payload;

    /// <summary>Asset factory that binds the store-assigned id.</summary>
    public required Func<AssetId, IAsset> AssetFactory { get; init; }

    /// <summary>Decoded payload; transferred to the store by <see cref="TakePayload"/>.</summary>
    public required IAssetPayload Payload
    {
        get => _payload ?? throw new InvalidOperationException("The draft payload was already transferred.");
        init => _payload = value;
    }

    /// <summary>Transfers payload ownership; subsequent <see cref="Dispose"/> calls are no-ops.</summary>
    internal IAssetPayload? TakePayload() => Interlocked.Exchange(ref _payload, null);

    public void Dispose() => Interlocked.Exchange(ref _payload, null)?.Dispose();
}
