using System.Diagnostics.CodeAnalysis;
using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Asset;

/// <summary>One-shot ownership of a decoded payload; safe to read and take from any thread.</summary>
internal sealed class PayloadLease : IDisposable
{
    private IAssetPayload _payload;

    private PayloadLease()
    {
        throw new InvalidOperationException();
    }

    internal PayloadLease(IAssetPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        _payload = payload;
    }

    /// <summary>Reads the payload without taking ownership.</summary>
    public bool TryPeek<T>(out T payload)
        where T : class, IAssetPayload
    {
        payload = (T)Volatile.Read(ref _payload);
        return payload is not null;
    }

    /// <summary>Takes ownership of the payload; only one caller succeeds and the lease is empty afterwards.</summary>
    public bool TryTake<T>(out T payload)
        where T : class, IAssetPayload
    {
        payload = null!;

        IAssetPayload current = Volatile.Read(ref _payload);
        if (current is not T)
            return false;

        // The winner is the caller whose exchange saw the same payload; everyone else observes the empty slot.
        if (!ReferenceEquals(Interlocked.CompareExchange(ref _payload!, null, current), current))
            return false;

        payload = (T)current;
        return true;
    }

    /// <summary>Releases the payload if it is still owned; calling this more than once is a no-op.</summary>
    public void Dispose()
    {
        IAssetPayload payload = Interlocked.Exchange(ref _payload!, null);
        if (payload is IDisposable disposable)
            disposable.Dispose();
    }
}
