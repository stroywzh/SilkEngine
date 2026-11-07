using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Core.AssetSystem;

/// <summary>
/// Asset state with exactly one owner thread: path map, runtime records, reference counts, deferred release and the
/// single published view snapshot other threads observe.
/// </summary>
/// <remarks>Registration, refcounting, release and publishing all happen here, so the single-writer rule holds.
/// <see cref="TryGet"/> and the ready/released queues are safe to read from any thread.</remarks>
internal sealed class AssetStore
{
    private readonly ConcurrentQueue<AssetId> _ready;
    private readonly ConcurrentQueue<AssetId> _released;

    private readonly Dictionary<string, Entry> _byPath = new(StringComparer.Ordinal);
    private readonly Dictionary<ulong, Entry> _byId = new();
    private readonly List<AssetId> _readyBuffer = new();
    private readonly List<AssetId> _releasedBuffer = new();

    private IReadOnlyDictionary<ulong, AssetView> _snapshot = new Dictionary<ulong, AssetView>();

    private ulong _lastId;
    private bool _dirty;
    private int _deferredCount;

    internal AssetStore(ConcurrentQueue<AssetId> ready, ConcurrentQueue<AssetId> released)
    {
        _ready = ready ?? throw new ArgumentNullException(nameof(ready));
        _released = released ?? throw new ArgumentNullException(nameof(released));
    }

    /// <summary>Number of assets whose refcount reached zero and that are waiting for their release window.</summary>
    internal int DeferredCount => Volatile.Read(ref _deferredCount);

    // Owner thread: reuse a cached asset (refcount +1, reviving a pending release) or report a miss.
    internal bool TryReuse(string path, [MaybeNullWhen(false)] out IAsset asset)
    {
        if (_byPath.TryGetValue(path, out Entry? entry))
        {
            if (entry.RefCount == 0 && entry.Deferred)
            {
                entry.Deferred = false;
                _deferredCount--;
            }

            entry.RefCount++;
            entry.DueAtMs = 0;
            asset = entry.Asset;
            return true;
        }

        asset = null;
        return false;
    }

    // Owner thread: reuse or decode synchronously; the caller observes the registered view before this returns.
    internal IAsset LoadNow(AssetSource source, IAssetLoader loader)
    {
        if (TryReuse(source.ProjectPath, out IAsset? cached))
            return cached!;

        IAsset asset = Register(source.ProjectPath, loader.Load(source));
        Commit();
        return asset;
    }

    // Owner thread: one record (asset + payload lease) under one id; the ready id is flushed by Commit.
    internal IAsset Register(string path, AssetDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        // Reject the duplicate before an id or a lease exists, so the failure leaks neither.
        if (_byPath.ContainsKey(path))
        {
            throw new InvalidOperationException($"Asset path '{path}' is already registered.");
        }

        ulong id = unchecked(++_lastId);
        if (id == 0)
            throw new InvalidOperationException("AssetStore: asset id space exhausted.");

        var assetId = new AssetId(id);
        var payload = new PayloadLease(draft.Payload);

        IAsset asset;
        try
        {
            asset = draft.AssetFactory(assetId);
        }
        catch
        {
            payload.Dispose();
            throw;
        }

        var entry = new Entry(assetId, path, asset, payload);
        _byId.Add(assetId.Value, entry);
        _byPath.Add(path, entry);
        _readyBuffer.Add(assetId);
        _dirty = true;

        return asset;
    }

    // Owner thread: refcount -1; zero either releases now or starts the grace window.
    internal bool Unload(AssetId assetId, long nowMs, bool defer, TimeSpan window)
    {
        if (!assetId.IsValid || !_byId.TryGetValue(assetId.Value, out Entry? entry))
            return false;

        if (entry.RefCount > 1)
        {
            entry.RefCount--;
            return true;
        }

        if (entry.RefCount == 0)
            return false;

        entry.RefCount = 0;

        if (!defer)
        {
            Release(entry);
            return true;
        }

        entry.DueAtMs = nowMs + (long)window.TotalMilliseconds;
        entry.Deferred = true;
        _deferredCount++;
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
            Release(entry);
    }

    /// <summary>Reads one published record; safe to call from any thread.</summary>
    internal bool TryGet(AssetId assetId, out AssetView view)
    {
        view = default;
        if (!assetId.IsValid)
            return false;

        return Volatile.Read(ref _snapshot).TryGetValue(assetId.Value, out view);
    }

    // Owner thread: the path map is not thread-safe.
    internal bool TryGetByPath<T>(string path, out T? asset)
        where T : IAsset
    {
        asset = default;
        if (!_byPath.TryGetValue(path, out Entry? entry))
            return false;

        if (entry.Asset is T typed)
        {
            asset = typed;
            return true;
        }

        return false;
    }

    /// <summary>Publishes the current records once, then flushes the buffered ready/release notifications.</summary>
    internal void Commit()
    {
        if (!_dirty)
            return;

        var snapshot = new Dictionary<ulong, AssetView>(_byId.Count);
        foreach (Entry entry in _byId.Values)
        {
            snapshot[entry.Id.Value] = new AssetView(entry.Id, entry.Asset, entry.Payload);
        }

        // Readers observe the new view before either queue announces it.
        Volatile.Write(ref _snapshot, snapshot);

        foreach (AssetId ready in _readyBuffer)
        {
            _ready.Enqueue(ready);
        }
        _readyBuffer.Clear();

        foreach (AssetId released in _releasedBuffer)
        {
            _released.Enqueue(released);
        }
        _releasedBuffer.Clear();

        _dirty = false;
    }

    private void Release(Entry entry)
    {
        _byPath.Remove(entry.Path);
        _byId.Remove(entry.Id.Value);
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
        internal Entry(AssetId id, string path, IAsset asset, PayloadLease payload)
        {
            Id = id;
            Path = path;
            Asset = asset;
            Payload = payload;
            RefCount = 1;
        }

        internal AssetId Id { get; }

        internal string Path { get; }

        internal IAsset Asset { get; }

        internal PayloadLease Payload { get; }

        internal int RefCount { get; set; }

        // Release deadline in the same clock as the worker's; 0 means "not in the window".
        internal long DueAtMs { get; set; }

        internal bool Deferred { get; set; }
    }
}
