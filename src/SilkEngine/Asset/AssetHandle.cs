using SilkEngine.Asset.Abstractions;
using SilkEngine.Core.AssetSystem;

namespace SilkEngine.Asset;

/// <summary>Ownership ticket for one loaded asset: its identity plus exactly one reference.</summary>
/// <remarks>Dispose releases the reference exactly once and may be called from any thread; the store's owner
/// thread applies the change at its next commit. Do not touch the asset after its ticket was disposed.
/// Two tickets for the same record are independent and never share their disposed state. The store retention
/// is a separate handle owned by the asset itself and released only by the internal unload/shutdown path;
/// caller tickets are released by the caller.</remarks>
public abstract class AssetHandle : IDisposable
{
    private int _disposed;

    internal AssetHandle(AssetId id, AssetLifetime lifetime)
    {
        Id = id;
        Lifetime = lifetime;
    }

    /// <summary>Identity of the asset this handle keeps alive.</summary>
    public AssetId Id { get; }

    /// <summary>Reference counter this handle contributes to; owned by the asset record.</summary>
    internal AssetLifetime Lifetime { get; }

    /// <summary>True until <see cref="Dispose"/> released the reference.</summary>
    public bool IsValid => Volatile.Read(ref _disposed) == 0;

    /// <summary>Releases this handle's reference exactly once; a second call is a no-op.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        OnReleased();
        ReleaseReference();
    }

    /// <summary>Drops cached asset references in the derived handle while it is being released.</summary>
    protected virtual void OnReleased() { }

    /// <summary>Releases the one reference this handle contributed.</summary>
    protected abstract void ReleaseReference();
}

/// <summary>Strongly typed caller ticket for <typeparamref name="T"/>; holds one reference while valid.</summary>
public sealed class AssetHandle<T> : AssetHandle
    where T : IAsset
{
    private T? _asset;

    internal AssetHandle(AssetId id, T asset, AssetLifetime lifetime)
        : base(id, lifetime) => _asset = asset;

    /// <summary>The loaded asset.</summary>
    /// <exception cref="ObjectDisposedException">The handle was already disposed.</exception>
    /// <exception cref="InvalidOperationException">The record no longer publishes this asset.</exception>
    public T Asset
    {
        get
        {
            if (!IsValid)
                throw new ObjectDisposedException(nameof(AssetHandle<T>));

            return _asset
                ?? throw new InvalidOperationException($"Asset {Id.Value} is no longer available.");
        }
    }

    /// <summary>Takes one more caller ticket and returns a second, independent handle to the same asset.</summary>
    /// <exception cref="ObjectDisposedException">This handle was already disposed.</exception>
    public AssetHandle<T> Clone()
    {
        if (!IsValid)
            throw new ObjectDisposedException(nameof(AssetHandle<T>));

        AssetRuntime.AcquireHandleRef(Id, Lifetime);
        return new AssetHandle<T>(Id, _asset!, Lifetime);
    }

    /// <inheritdoc />
    protected override void OnReleased() => _asset = default;

    /// <inheritdoc />
    protected override void ReleaseReference() => AssetRuntime.ReleaseHandle(Id, Lifetime);
}

/// <summary>Store-owned retention handle: keeps a record alive without publishing the asset.</summary>
/// <remarks>Created by the store and carried by the asset itself; only the internal unload and shutdown paths
/// release it.</remarks>
internal sealed class AssetRetentionHandle : AssetHandle
{
    internal AssetRetentionHandle(AssetId id, AssetLifetime lifetime)
        : base(id, lifetime) { }

    /// <inheritdoc />
    protected override void ReleaseReference() => AssetRuntime.ReleaseRetention(Id, Lifetime);
}
