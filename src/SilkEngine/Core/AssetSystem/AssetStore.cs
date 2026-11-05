using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;

namespace SilkEngine.Core.AssetSystem;

/// <summary>
/// Asset state with exactly one owner thread: path map, asset cache, reference counts, deferred release and the
/// read-only snapshots other threads observe.
/// </summary>
/// <remarks><see cref="AssetTable"/> writes, snapshot publishing and the ready/release notifications all happen
/// here, so the single-writer requirement holds for both the asset thread and the inline mode.
/// <see cref="Snapshot"/> and the ready/release queues are safe to read from any thread.</remarks>
internal sealed class AssetStore
{
    private readonly ConcurrentQueue<AssetId> _ready;
    private readonly ConcurrentQueue<AssetId> _released;

    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly Dictionary<ulong, Entry> _entriesById = new();

    private IReadOnlyDictionary<ulong, IAsset> _snapshot = new Dictionary<ulong, IAsset>();

    private int _deferredCount;

    internal AssetStore(ConcurrentQueue<AssetId> ready, ConcurrentQueue<AssetId> released)
    {
        _ready = ready ?? throw new ArgumentNullException(nameof(ready));
        _released = released ?? throw new ArgumentNullException(nameof(released));
    }

    /// <summary>Read-only snapshot of the loaded assets; readable from any thread.</summary>
    internal IReadOnlyDictionary<ulong, IAsset> Snapshot => Volatile.Read(ref _snapshot);

    /// <summary>Number of assets whose refcount reached zero and that are waiting for their release window.</summary>
    internal int DeferredCount => Volatile.Read(ref _deferredCount);

    // Owner thread: reuse a cached asset (refcount +1, reviving a pending release) or report a miss.
    internal bool TryReuse(string path, [MaybeNullWhen(false)] out IAsset asset)
    {
        if (_entries.TryGetValue(path, out Entry? entry))
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

    // Owner thread: reuse or decode synchronously. Used by inline mode and by thread-internal loads.
    internal IAsset LoadNow(string path, IAssetLoader loader)
    {
        if (TryReuse(path, out IAsset? cached))
            return cached!;

        return Register(path, loader.Load(path));
    }

    // Owner thread: register the decoded payload, bind the asset id, publish snapshots and enqueue ready.
    internal IAsset Register(string path, AssetDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        AssetId assetId = AssetTable.Register(new RawDataContainer(draft.Payload));
        IAsset asset = draft.Asset.WithAssetId(assetId);

        var entry = new Entry(path, asset);
        _entries.Add(path, entry);
        _entriesById.Add(assetId.Value, entry);
        PublishSnapshot();
        _ready.Enqueue(assetId);

        return asset;
    }

    // Owner thread: refcount -1; zero either releases now or starts the grace window.
    internal bool Unload(AssetId assetId, long nowMs, bool defer, TimeSpan window)
    {
        if (!assetId.IsValid || !_entriesById.TryGetValue(assetId.Value, out Entry? entry))
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
        foreach (Entry entry in _entriesById.Values)
        {
            if (entry.Deferred && entry.DueAtMs <= nowMs)
                (due ??= new List<Entry>()).Add(entry);
        }

        if (due is null)
            return;

        foreach (Entry entry in due)
            Release(entry);
    }

    internal bool TryGetById<T>(AssetId assetId, out T? asset)
        where T : IAsset
    {
        asset = default;
        if (!assetId.IsValid)
            return false;

        if (!Snapshot.TryGetValue(assetId.Value, out IAsset? found))
            return false;

        if (found is T typed)
        {
            asset = typed;
            return true;
        }

        return false;
    }

    // Owner thread: the path map is not thread-safe.
    internal bool TryGetByPath<T>(string path, out T? asset)
        where T : IAsset
    {
        asset = default;
        if (!_entries.TryGetValue(path, out Entry? entry))
            return false;

        if (entry.Asset is T typed)
        {
            asset = typed;
            return true;
        }

        return false;
    }

    // Maps first, then the table snapshot, then the release notification: a released id is never observable later.
    private void Release(Entry entry)
    {
        _entries.Remove(entry.Path);
        _entriesById.Remove(entry.AssetId.Value);
        PublishSnapshot();
        AssetTable.Unregister(entry.AssetId);
        _released.Enqueue(entry.AssetId);

        if (entry.Deferred)
        {
            entry.Deferred = false;
            _deferredCount--;
        }
    }

    // Copy-on-publish: readers keep a stable snapshot; it is visible before the ready id is enqueued.
    private void PublishSnapshot()
    {
        var snapshot = new Dictionary<ulong, IAsset>(_entries.Count);
        foreach (Entry entry in _entries.Values)
            snapshot[entry.AssetId.Value] = entry.Asset;

        Volatile.Write(ref _snapshot, snapshot);
    }

    private sealed class Entry
    {
        internal Entry(string path, IAsset asset)
        {
            Path = path;
            Asset = asset;
            RefCount = 1;
        }

        internal string Path { get; }

        internal IAsset Asset { get; }

        internal AssetId AssetId => Asset.AssetId;

        internal int RefCount { get; set; }

        // Release deadline in the same clock as the worker's; 0 means "not in the window".
        internal long DueAtMs { get; set; }

        internal bool Deferred { get; set; }
    }
}
