using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Core.AssetSystem;

/// <summary>
/// Asset state with exactly one owner thread: path map, runtime records, reference counts, deferred release and the
/// single published view snapshot other threads observe.
/// </summary>
/// <remarks>The owner is the engine main loop. A record starts with one cache pin; render-side acquisitions add
/// references through <see cref="AssetLifetime"/>, and the record is removed only when the pin and every render
/// reference are gone. Releasing the pin while render references remain announces the id through the pin-released
/// queue so the render side can destroy its bindings and release those references.</remarks>
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

    /// <summary>Number of assets whose cache pin is released and that are waiting for their release window.</summary>
    internal int DeferredCount => Volatile.Read(ref _deferredCount);

    // Owner thread: reuse a cached asset. A normal cache hit does not add a pin/reference; a record whose pin
    // was released or whose release window started is revived by taking the pin back.
    internal bool TryReuse(string path, [MaybeNullWhen(false)] out IAsset asset)
    {
        asset = null;
        if (!_byPath.TryGetValue(path, out Entry? entry))
            return false;

        if (!entry.Lifetime.PinHeld)
        {
            if (!entry.Lifetime.TryPin())
                return false;

            if (entry.Deferred)
            {
                entry.Deferred = false;
                _deferredCount--;
            }

            entry.DueAtMs = 0;
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

            // A duplicate load may race a pin release; revive the pin so the returned asset remains valid.
            if (!existing.Lifetime.PinHeld && existing.Lifetime.TryPin())
            {
                if (existing.Deferred)
                {
                    existing.Deferred = false;
                    _deferredCount--;
                }

                existing.DueAtMs = 0;
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

        IAsset asset;
        try
        {
            asset = draft.AssetFactory(assetId);
        }
        catch
        {
            lease.Dispose();
            throw;
        }

        var entry = new Entry(assetId, path, asset, lease, new AssetLifetime());
        _byId.Add(assetId.Value, entry);
        _byPath.Add(path, entry);
        _readyBuffer.Add(assetId);
        _dirty = true;
        added = true;

        return asset;
    }

    // Owner thread: release the cache pin. Render references keep the record alive until they are released;
    // when they are present the id is announced on the pin-released queue first.
    internal bool Unload(AssetId assetId, long nowMs, bool defer, TimeSpan window)
    {
        if (!assetId.IsValid || !_byId.TryGetValue(assetId.Value, out Entry? entry))
            return false;

        if (!entry.Lifetime.ReleasePin())
            return false;

        if (entry.Lifetime.Count == 0)
        {
            if (!defer)
            {
                RemoveEntry(entry);
                return true;
            }

            entry.DueAtMs = nowMs + (long)window.TotalMilliseconds;
            entry.Deferred = true;
            _deferredCount++;
            return true;
        }

        _pinReleasedBuffer.Add(entry.Id);
        _dirty = true;
        return true;
    }

    // Owner thread: release one render-side reference. Only render references are touched; when the pin is
    // already released and this was the last reference the record leaves the published snapshot.
    internal bool ReleaseRenderRef(AssetId assetId)
    {
        if (!assetId.IsValid || !_byId.TryGetValue(assetId.Value, out Entry? entry))
            return false;

        if (!entry.Lifetime.ReleaseRenderRef())
            return false;

        if (entry.Lifetime.Count == 0 && !entry.Lifetime.PinHeld)
        {
            if (entry.Deferred)
            {
                entry.Deferred = false;
                _deferredCount--;
            }

            RemoveEntry(entry);
        }

        return true;
    }

    // Owner thread: release every entry whose grace window has elapsed.
    internal void ProcessDue(long nowMs)
    {
        if (_deferredCount <= 0)
            return;

        List<Entry>? due = null;
        foreach (Entry entry in _byId.Values)
        {
            if (entry.Deferred && entry.DueAtMs <= nowMs)
                (due ??= new List<Entry>()).Add(entry);
        }

        if (due is null)
            return;

        foreach (Entry entry in due)
        {
            entry.Deferred = false;
            _deferredCount--;
            RemoveEntry(entry);
        }
    }

    /// <summary>Reads one published record; safe to call from any thread.</summary>
    internal bool TryGet(AssetId assetId, out AssetView view)
    {
        view = default;
        if (!assetId.IsValid)
            return false;

        return Volatile.Read(ref _snapshot).TryGetValue(assetId.Value, out view);
    }

    /// <summary>Publishes the current records once, then flushes the buffered ready/release notifications.</summary>
    internal void Commit()
    {
        if (!_dirty)
            return;

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

    private void RemoveEntry(Entry entry)
    {
        if (!_byPath.Remove(entry.Path))
            return;

        _byId.Remove(entry.Id.Value);
        entry.Lifetime.MarkRemoved();
        entry.Payload.Dispose();
        _releasedBuffer.Add(entry.Id);
        _dirty = true;

        if (entry.Deferred)
        {
            entry.Deferred = false;
            _deferredCount--;
        }
    }

    private sealed class Entry
    {
        internal Entry(
            AssetId id,
            string path,
            IAsset asset,
            PayloadLease payload,
            AssetLifetime lifetime
        )
        {
            Id = id;
            Path = path;
            Asset = asset;
            Payload = payload;
            Lifetime = lifetime;
        }

        internal AssetId Id { get; }

        internal string Path { get; }

        internal IAsset Asset { get; }

        internal PayloadLease Payload { get; }

        internal AssetLifetime Lifetime { get; }

        // Release deadline in the same clock as the owner loop's; 0 means "not in the window".
        internal long DueAtMs { get; set; }

        internal bool Deferred { get; set; }
    }
}

