using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Core.AssetSystem;

/// <summary>
/// Asset state with exactly one owner thread: path map, runtime records, reference counts, deferred release and the
/// single published view snapshot other threads observe.
/// </summary>
/// <remarks>The owner is the engine main loop. A record starts with one store retention; render-side acquisitions
/// add references through <see cref="AssetLifetime"/>, and the record is removed only when the retention and every
/// ticket and render reference are gone. Other threads only change counters; the owner scans them once per commit,
/// announces a teardown through the release queue when only render references are left, and removes a record after
/// its release window elapsed.</remarks>
internal sealed class AssetStore
{
    private readonly ConcurrentQueue<AssetId> _ready;
    private readonly ConcurrentQueue<AssetId> _released;
    private readonly ConcurrentQueue<AssetId> _pinReleased;

    private readonly Dictionary<string, Entry> _byPath = new(StringComparer.Ordinal);
    private readonly Dictionary<ulong, Entry> _byId = new();
    private readonly List<AssetId> _readyBuffer = new();
    private readonly List<AssetId> _releasedBuffer = new();
    private readonly List<AssetId> _pinReleasedBuffer = new();

    // Owner-thread clock used by release windows; the asset pump advances it on every pass.
    private double _elapsedMs;

    // Payload leases released by a removal; disposed only after the new snapshot was published.
    private readonly List<PayloadLease> _payloadDisposals = new();

    private IReadOnlyDictionary<ulong, AssetView> _snapshot = new Dictionary<ulong, AssetView>();

    private ulong _lastId;
    private bool _dirty;
    private int _deferredCount;

    internal AssetStore(
        ConcurrentQueue<AssetId> ready,
        ConcurrentQueue<AssetId> released,
        ConcurrentQueue<AssetId> pinReleased
    )
    {
        _ready = ready ?? throw new ArgumentNullException(nameof(ready));
        _released = released ?? throw new ArgumentNullException(nameof(released));
        _pinReleased = pinReleased ?? throw new ArgumentNullException(nameof(pinReleased));
    }

    /// <summary>Creates a store whose teardown notifications are dropped; tools and legacy tests only.</summary>
    internal AssetStore(ConcurrentQueue<AssetId> ready, ConcurrentQueue<AssetId> released)
        : this(ready, released, new ConcurrentQueue<AssetId>()) { }

    /// <summary>Number of assets whose store retention is released and that are waiting for their release window.</summary>
    internal int DeferredCount => Volatile.Read(ref _deferredCount);

    // Owner thread: advances the store clock that release windows are measured against.
    internal void Advance(double deltaSeconds)
    {
        if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0)
            return;

        _elapsedMs += deltaSeconds * 1000.0;
    }

    // Owner thread: reuse a cached asset. A normal cache hit does not add a reference; a record whose retention
    // was released or whose release window started is revived by taking the retention back.
    internal bool TryReuse(string path, [MaybeNullWhen(false)] out IAsset asset)
    {
        asset = null;
        if (!_byPath.TryGetValue(path, out Entry? entry))
            return false;

        if (!entry.Lifetime.RetentionHeld)
        {
            if (!TryReviveRetention(entry))
                return false;
        }

        if (entry.Lifetime.Count <= 0)
            return false;

        asset = entry.Asset;
        return true;
    }

    // Owner thread: reuse or decode synchronously; the caller observes the registered view before this returns.
    internal IAsset LoadNow(AssetSource source, IAssetLoader loader)
    {
        if (TryReuse(source.ProjectPath, out IAsset? cached))
            return cached!;

        IAsset asset = Register(source.ProjectPath, loader.Load(source), out _);
        Commit();
        return asset;
    }

    // Owner thread: register a draft. A duplicate path reuses the existing record and disposes the duplicate
    // draft instead of throwing; only a newly added record consumes an id and takes payload ownership.
    internal IAsset Register(string path, AssetDraft draft) => Register(path, draft, out _);

    internal IAsset Register(string path, AssetDraft draft, out bool added)
    {
        ArgumentNullException.ThrowIfNull(draft);

        if (_byPath.TryGetValue(path, out Entry? existing))
        {
            draft.Dispose();

            // A duplicate load restores the retention whenever it was released; tickets/render refs stay as-is.
            if (!existing.Lifetime.RetentionHeld)
            {
                TryReviveRetention(existing);
            }

            added = false;
            return existing.Asset;
        }

        ulong id = unchecked(++_lastId);
        if (id == 0)
        {
            draft.Dispose();
            throw new InvalidOperationException("AssetStore: asset id space exhausted.");
        }

        IAssetPayload? payload = draft.TakePayload();
        if (payload is null)
        {
            draft.Dispose();
            throw new InvalidOperationException($"Asset draft for '{path}' carried no payload.");
        }

        var assetId = new AssetId(id);
        var lease = new PayloadLease(payload);
        var lifetime = new AssetLifetime();
        var retention = new AssetRetentionHandle(assetId, lifetime);

        IAsset asset;
        try
        {
            asset = draft.AssetFactory(retention);
        }
        catch
        {
            draft.Dispose();
            retention.Dispose();
            lease.Dispose();
            throw;
        }

        if (asset is not AssetBase)
        {
            draft.Dispose();
            retention.Dispose();
            lease.Dispose();
            throw new InvalidOperationException("asset factory must return AssetBase");
        }

        var entry = new Entry(assetId, path, asset, lease, lifetime, retention);
        _byId.Add(assetId.Value, entry);
        _byPath.Add(path, entry);
        _readyBuffer.Add(assetId);
        _dirty = true;
        added = true;

        return asset;
    }

    // Owner thread: releases the store retention. Caller tickets and render bindings keep the record alive; when
    // only render references are left the id is announced so the render side can drop its bindings.
    internal bool Unload(AssetId assetId, long nowMs, bool defer, TimeSpan window)
    {
        if (!assetId.IsValid || !_byId.TryGetValue(assetId.Value, out Entry? entry))
            return false;

        // The store-owned retention is idempotent; a second unload has nothing left to release.
        if (!entry.Retention.IsValid)
            return false;

        entry.Retention.Dispose();

        if (entry.Lifetime.NonRenderRefs > 0)
            return true;

        if (entry.Lifetime.Count == 0)
        {
            DeferRemoval(entry, defer ? window : TimeSpan.Zero);
            return true;
        }

        AnnounceTeardown(entry);
        return true;
    }

    // Owner thread: release one render-side reference. Only render references are touched; when this was the last
    // reference the removal is deferred to the next scan, which owns the removal decision.
    internal bool ReleaseRenderRef(AssetId assetId)
    {
        if (!assetId.IsValid || !_byId.TryGetValue(assetId.Value, out Entry? entry))
            return false;

        if (!entry.Lifetime.ReleaseRenderRef())
            return false;

        if (entry.Lifetime.Count == 0)
            DeferRemoval(entry, TimeSpan.Zero);
        else if (entry.Deferred)
            CancelDeferred(entry);

        return true;
    }

    // Owner thread: derives every record's pending action from its current reference counts. Handles only change
    // counters, so this scan is the single place that announces teardown, opens release windows and removes
    // records; the removal fence itself stays atomic.
    internal void Scan()
    {
        if (_byId.Count == 0)
            return;

        List<Entry>? due = null;
        foreach (Entry entry in _byId.Values)
        {
            if (entry.Lifetime.IsRemoved)
                continue;

            // Only render references are left: ask the render side to drop its bindings.
            if (
                !entry.Lifetime.RetentionHeld
                && entry.Lifetime.NonRenderRefs == 0
                && entry.Lifetime.RenderRefCount > 0
            )
            {
                AnnounceTeardown(entry);
            }

            if (entry.Lifetime.Count > 0)
            {
                // A reference arrived inside the release window; keep the record published.
                if (entry.Deferred)
                    CancelDeferred(entry);

                // The binding may already have been torn down; re-announce so the render side uploads again.
                if (entry.TeardownAnnounced && entry.Lifetime.RenderRefCount == 0)
                    Reannounce(entry);

                continue;
            }

            if (!entry.Deferred)
                DeferRemoval(entry, TimeSpan.Zero);

            if (entry.DueAtMs <= _elapsedMs)
                (due ??= new List<Entry>()).Add(entry);
        }

        if (due is null)
            return;

        // Removal mutates the dictionary, so it runs after the enumeration.
        foreach (Entry entry in due)
            RemoveEntry(entry);
    }

    /// <summary>Releases every retention the store and its assets still hold; shutdown only.</summary>
    internal void ReleaseAllRetention()
    {
        var entries = new Entry[_byId.Count];
        _byId.Values.CopyTo(entries, 0);

        foreach (Entry entry in entries)
        {
            if (entry.Lifetime.IsRemoved)
                continue;

            entry.Retention.Dispose();
        }

        // Removals are derived by the scan inside the commit, so the snapshot and payloads settle together.
        Commit();
    }

    /// <summary>Reads one published record; safe to call from any thread.</summary>
    internal bool TryGet(AssetId assetId, out AssetView view)
    {
        view = default;
        if (!assetId.IsValid)
            return false;

        return Volatile.Read(ref _snapshot).TryGetValue(assetId.Value, out view);
    }

    /// <summary>Scans records, publishes pending changes, then releases the payloads removed by this pass.</summary>
    /// <remarks>Payload disposal deliberately follows the snapshot publish: readers observing the new snapshot can
    /// never reach a removed record whose lease was already disposed.</remarks>
    internal void Commit()
    {
        Scan();

        if (_dirty)
        {
            PublishPendingChanges();
        }

        FlushPayloadDisposals();
    }

    private void PublishPendingChanges()
    {
        var snapshot = new Dictionary<ulong, AssetView>(_byId.Count);
        foreach (Entry entry in _byId.Values)
        {
            snapshot[entry.Id.Value] = new AssetView(
                entry.Id,
                entry.Asset,
                entry.Payload,
                entry.Lifetime
            );
        }

        // Readers observe the new view before either queue announces it.
        Volatile.Write(ref _snapshot, snapshot);

        foreach (AssetId ready in _readyBuffer)
        {
            _ready.Enqueue(ready);
        }
        _readyBuffer.Clear();

        foreach (AssetId pinReleased in _pinReleasedBuffer)
        {
            _pinReleased.Enqueue(pinReleased);
        }
        _pinReleasedBuffer.Clear();

        foreach (AssetId released in _releasedBuffer)
        {
            _released.Enqueue(released);
        }
        _releasedBuffer.Clear();

        _dirty = false;
    }

    private void FlushPayloadDisposals()
    {
        if (_payloadDisposals.Count == 0)
            return;

        foreach (PayloadLease lease in _payloadDisposals)
        {
            lease.Dispose();
        }

        _payloadDisposals.Clear();
    }

    // Owner thread: restores the retention of a fully released record and republishes it so the render side can
    // bind the asset again.
    private bool TryReviveRetention(Entry entry)
    {
        if (!entry.Lifetime.TryReviveRetention())
            return false;

        var retention = new AssetRetentionHandle(entry.Id, entry.Lifetime);
        entry.Retention = retention;

        if (entry.Asset is AssetBase assetBase)
        {
            assetBase.AttachHandle(retention);
        }

        if (entry.Deferred)
        {
            entry.Deferred = false;
            _deferredCount--;
        }

        entry.DueAtMs = 0;
        Reannounce(entry);
        return true;
    }

    private void DeferRemoval(Entry entry, TimeSpan window)
    {
        if (entry.Deferred)
            return;

        entry.DueAtMs = (long)(_elapsedMs + window.TotalMilliseconds);
        entry.Deferred = true;
        _deferredCount++;
    }

    private void CancelDeferred(Entry entry)
    {
        if (!entry.Deferred)
            return;

        entry.Deferred = false;
        entry.DueAtMs = 0;
        _deferredCount--;
    }

    private void AnnounceTeardown(Entry entry)
    {
        if (entry.TeardownAnnounced)
            return;

        entry.TeardownAnnounced = true;
        _pinReleasedBuffer.Add(entry.Id);
        _dirty = true;
    }

    // Re-publishes a revived record: the render side may have destroyed its binding already.
    private void Reannounce(Entry entry)
    {
        entry.TeardownAnnounced = false;
        _readyBuffer.Add(entry.Id);
        _dirty = true;
    }

    private void RemoveEntry(Entry entry)
    {
        if (
            !_byPath.TryGetValue(entry.Path, out Entry? current) || !ReferenceEquals(current, entry)
        )
            return;

        // Latch removal atomically: a reference taken in between keeps the record alive.
        if (!entry.Lifetime.TryMarkRemovedIfUnused())
        {
            CancelDeferred(entry);
            return;
        }

        _byPath.Remove(entry.Path);
        _byId.Remove(entry.Id.Value);
        _payloadDisposals.Add(entry.Payload);
        _releasedBuffer.Add(entry.Id);
        _dirty = true;
        CancelDeferred(entry);
    }

    private sealed class Entry
    {
        internal Entry(
            AssetId id,
            string path,
            IAsset asset,
            PayloadLease payload,
            AssetLifetime lifetime,
            AssetHandle retention
        )
        {
            Id = id;
            Path = path;
            Asset = asset;
            Payload = payload;
            Lifetime = lifetime;
            Retention = retention;
        }

        internal AssetId Id { get; }

        internal string Path { get; }

        internal IAsset Asset { get; }

        internal PayloadLease Payload { get; }

        internal AssetLifetime Lifetime { get; }

        // Store-owned retention; replaced when a released record is revived.
        internal AssetHandle Retention { get; set; }

        // Release deadline on the store clock; meaningful only while Deferred is set.
        internal long DueAtMs { get; set; }

        internal bool Deferred { get; set; }

        // True once the retention-release notification was queued for this release cycle.
        internal bool TeardownAnnounced { get; set; }
    }
}
