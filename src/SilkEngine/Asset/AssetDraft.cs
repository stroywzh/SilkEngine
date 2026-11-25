using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Asset;

/// <summary>Decoded asset content produced by a loader and registered by the engine.</summary>
/// <remarks>Ownership of <see cref="Payload"/> transfers to the store only after a successful registration;
/// every other path (duplicate path, cancel, failure) must dispose the draft.</remarks>
internal sealed class AssetDraft : IDisposable
{
    internal AssetDraft(IAssetPayload payload)
    {
        _payload = payload;
    }

    private IAssetPayload _payload;
    private bool IsVaild => Volatile.Read(ref _payload) is not null;

    /// <summary>Asset factory that binds the store-issued handle.</summary>
    public required Func<AssetHandle, IAsset> AssetFactory { get; init; }

    /// <summary>Decoded payload; transferred to the store by <see cref="TakePayload"/>.</summary>
    public IAssetPayload Payload
    {
        get =>
            IsVaild
                ? _payload
                : throw new InvalidOperationException("The draft payload was already transferred.");
        private set => _payload = value;
    }

    /// <summary>Transfers payload ownership; subsequent <see cref="Dispose"/> calls are no-ops.</summary>
    internal IAssetPayload TakePayload()
    {
        if (IsVaild)
        {
            var payload = Interlocked.Exchange(ref _payload, null!);
            return payload;
        }
        throw new InvalidOperationException("The draft payload was already transferred.");
    }

    // 这里Dispose是不是有点太傻逼了，我感觉只要是能TakePayload就可以直接Dispose？
    // TakePayload 之后payload就是null?
    // 但是也可以做成不检查IsVaild
    public void Dispose() => Interlocked.Exchange(ref _payload!, null)?.Dispose();
}
