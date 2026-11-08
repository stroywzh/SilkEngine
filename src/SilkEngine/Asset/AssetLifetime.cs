namespace SilkEngine.Asset;

/// <summary>Reference counts of one registered asset: store retention, caller tickets and render bindings.</summary>
/// <remarks>All counters change under one lock so the store's owner thread can decide removal atomically.
/// A record is removable only while every counter is zero; <see cref="TryMarkRemovedIfUnused"/> latches that
/// decision and fails when a reference arrived first. Ticket and render releases never consume the retention.
/// The retention starts held by the store; only its unload/shutdown path releases it, and a cache hit revives
/// it through <see cref="TryReviveRetention"/>.</remarks>
internal sealed class AssetLifetime
{
    private readonly object _gate = new();
    private int _retention = 1;
    private int _tickets;
    private int _renderRefs;
    private bool _removed;

    /// <summary>Total references: store retention plus caller tickets and render bindings.</summary>
    internal int Count
    {
        get
        {
            lock (_gate)
                return _retention + _tickets + _renderRefs;
        }
    }

    /// <summary>References owned outside the render side: store retention plus caller tickets.</summary>
    internal int NonRenderRefs
    {
        get
        {
            lock (_gate)
                return _retention + _tickets;
        }
    }

    /// <summary>True while the store retention is held; only the store's unload/shutdown path releases it.</summary>
    internal bool RetentionHeld
    {
        get
        {
            lock (_gate)
                return _retention != 0;
        }
    }

    /// <summary>Number of render-side references currently held.</summary>
    internal int RenderRefCount
    {
        get
        {
            lock (_gate)
                return _renderRefs;
        }
    }

    /// <summary>True once the record was latched as removed; no reference can be taken afterwards.</summary>
    internal bool IsRemoved
    {
        get
        {
            lock (_gate)
                return _removed;
        }
    }

    /// <summary>Takes a render-side reference; fails after removal or while the record holds no reference.</summary>
    internal bool TryAcquire()
    {
        lock (_gate)
        {
            if (_removed || _retention + _tickets + _renderRefs == 0)
                return false;

            _renderRefs++;
            return true;
        }
    }

    /// <summary>Takes one caller ticket; fails after removal.</summary>
    internal bool TryAcquireTicket()
    {
        lock (_gate)
        {
            if (_removed)
                return false;

            _tickets++;
            return true;
        }
    }

    /// <summary>Restores the store retention after a load revived a fully or partly released record.</summary>
    internal bool TryReviveRetention()
    {
        lock (_gate)
        {
            if (_removed || _retention != 0)
                return false;

            _retention = 1;
            return true;
        }
    }

    /// <summary>Releases the store retention; fails when it was already released or the record was removed.</summary>
    internal bool ReleaseRetention()
    {
        lock (_gate)
        {
            if (_removed || _retention == 0)
                return false;

            _retention = 0;
            return true;
        }
    }

    /// <summary>Releases one caller ticket; fails when none is held or the record was removed.</summary>
    internal bool ReleaseTicket()
    {
        lock (_gate)
        {
            if (_removed || _tickets == 0)
                return false;

            _tickets--;
            return true;
        }
    }

    /// <summary>Releases one render-side reference; fails when none is held (the other counters are never touched).</summary>
    internal bool ReleaseRenderRef()
    {
        lock (_gate)
        {
            if (_removed || _renderRefs == 0)
                return false;

            _renderRefs--;
            return true;
        }
    }

    /// <summary>Latches removal when every counter is zero; returns false when a reference is still held.</summary>
    internal bool TryMarkRemovedIfUnused()
    {
        lock (_gate)
        {
            if (_removed || _retention != 0 || _tickets != 0 || _renderRefs != 0)
                return false;

            _removed = true;
            return true;
        }
    }
}
