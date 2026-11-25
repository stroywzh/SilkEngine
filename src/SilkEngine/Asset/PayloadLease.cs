using System.Diagnostics.CodeAnalysis;
using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Asset;

/// <summary>One-shot ownership of a decoded payload; safe to read and take from any thread.</summary>
/// <remarks>The lease holds exactly one payload. <see cref="TryTake{T}"/> uses a compare-exchange so only one
/// caller wins; everyone else observes an empty lease and gets <c>false</c>. <see cref="Dispose"/> releases the
/// payload only if it is still owned, is idempotent, and is safe from any thread. After a successful take or
/// dispose, <see cref="TryPeek{T}"/> and <see cref="TryTake{T}"/> return <c>false</c>.</remarks>
internal sealed class PayloadLease : IDisposable
{
    private IAssetPayload? _payload;

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
        if (Volatile.Read(ref _payload) is T typed)
        {
            payload = typed;
            return true;
        }

        payload = null!;
        return false;
    }

    /// <summary>Takes ownership of the payload; only one caller succeeds and the lease is empty afterwards.</summary>
    public bool TryTake<T>(out T payload)
        where T : class, IAssetPayload
    {
        payload = null!;

        if (Volatile.Read(ref _payload) is not T current)
            return false;

        // The winner is the caller whose exchange saw the same payload; everyone else observes the empty slot.
        if (!ReferenceEquals(Interlocked.CompareExchange(ref _payload, null, current), current))
            return false;

        payload = current;
        return true;
    }

    /// <summary>Releases the payload if it is still owned; calling this more than once is a no-op.</summary>
    public void Dispose() => Interlocked.Exchange(ref _payload, null)?.Dispose();
}
