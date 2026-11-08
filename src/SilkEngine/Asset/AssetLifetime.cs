namespace SilkEngine.Asset;

/// <summary>Reference count of one registered asset: one cache pin plus render-side acquisitions.</summary>
/// <remarks>The cache pin and render references are tracked separately so a render release can never consume the
/// pin. The owner removes the record only after the pin is released and the last render reference is gone.</remarks>
internal sealed class AssetLifetime
{
    private readonly object _gate = new();
    private int _pin = 1;
    private int _renderRefs;
    private bool _removed;

    /// <summary>Total references: the cache pin plus render-side acquisitions.</summary>
    internal int Count
    {
        get
        {
            lock (_gate)
                return _pin + _renderRefs;
        }
    }

    /// <summary>True while the cache pin is held; a released pin revives only through <see cref="TryPin"/>.</summary>
    internal bool PinHeld
    {
        get
        {
            lock (_gate)
                return _pin != 0;
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

    internal bool IsRemoved
    {
        get
        {
            lock (_gate)
                return _removed;
        }
    }

    /// <summary>Takes a render-side reference; fails after removal or after the cache pin was released.</summary>
    internal bool TryAcquire()
    {
        lock (_gate)
        {
            if (_removed || _pin == 0)
                return false;

            _renderRefs++;
            return true;
        }
    }

    /// <summary>Restores the cache pin after a cache hit revived a released/deferred record.</summary>
    internal bool TryPin()
    {
        lock (_gate)
        {
            if (_removed || _pin != 0)
                return false;

            _pin = 1;
            return true;
        }
    }

    /// <summary>Releases the cache pin; fails when it was already released or the record was removed.</summary>
    internal bool ReleasePin()
    {
        lock (_gate)
        {
            if (_removed || _pin == 0)
                return false;

            _pin = 0;
            return true;
        }
    }

    /// <summary>Releases one render-side reference; fails when none is held (the pin is never touched).</summary>
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

    internal void MarkRemoved()
    {
        lock (_gate)
            _removed = true;
    }
}
