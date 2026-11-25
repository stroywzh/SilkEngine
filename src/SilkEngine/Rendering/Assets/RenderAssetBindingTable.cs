using System.Collections.Concurrent;
using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;
using SilkEngine.AssetSystem.Runtime;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.Assets;

//TODO: 可优化点：一大堆Lock，我感觉有不少都是不必要的。

/// <summary>Render-thread table of device bindings for loaded assets; owns and releases every binding it creates.</summary>
/// <remarks>Asset identity stays behind <see cref="IRenderAssetSource"/>: pipelines and scene code only exchange
/// <see cref="RenderHandle"/> values. Upload failures are reported through the metrics and, for textures, a shared
/// 1x1 white fallback; only device contract failures escape into the render loop and fail the frame.</remarks>
public sealed class RenderAssetBindingTable : IRenderResourceTable
{
    private const int Rgba32BytesPerPixel = 4;
    private static readonly byte[] WhitePixel = [0xFF, 0xFF, 0xFF, 0xFF];

    private readonly IRenderAssetSource _source;
    private readonly Dictionary<Type, IAssetUploader> _uploaders = new();
    private readonly object _gate = new();
    private readonly List<BindingSlot> _slots = new();
    private readonly List<int> _freeSlots = new();
    private readonly Dictionary<AssetId, RenderHandle> _handles = new();
    private readonly ConcurrentQueue<RenderHandle> _added = new();
    private readonly ConcurrentQueue<RenderHandle> _removed = new();
    private readonly List<AssetId> _pending = new();
    private readonly Dictionary<AssetId, string> _failed = new();

    private IRenderDevice? _device;
    private FallbackTextureBinding? _fallback;
    private bool _disposed;
    private int _boundCount;
    private int _destroyFailures;
    private int _uploadFailures;
    private int _dropped;
    private int _budgetDeferred;

    /// <summary>Creates the render asset binding table over the asset bridge and the built-in uploaders.</summary>
    /// TODO:这里之后也可以把这个Uploader解耦掉，这里现在是硬编码的
    public RenderAssetBindingTable()
        : this(SilkEngine.AssetSystem.Runtime.AssetRenderBridge.Instance, new[] { new TextureUploader() }) { }

    /// <summary>Creates a table over an explicit bridge and uploader set; tests and custom compositions only.</summary>
    /// <exception cref="ArgumentException">Two uploaders register the same asset type.</exception>
    internal RenderAssetBindingTable(IRenderAssetSource source, IEnumerable<IAssetUploader> uploaders)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(uploaders);

        _source = source;
        foreach (IAssetUploader uploader in uploaders)
        {
            ArgumentNullException.ThrowIfNull(uploader);

            if (!_uploaders.TryAdd(uploader.AssetType, uploader))
                throw new ArgumentException(
                    $"An uploader for '{uploader.AssetType}' is already registered.",
                    nameof(uploaders)
                );
        }
    }

    /// <inheritdoc />
    public int BoundCount
    {
        get
        {
            lock (_gate)
                return _boundCount;
        }
    }

    /// <inheritdoc />
    public int PendingCount
    {
        get
        {
            lock (_gate)
                return _pending.Count;
        }
    }

    /// <inheritdoc />
    public int FailedCount
    {
        get
        {
            lock (_gate)
                return _failed.Count;
        }
    }

    /// <summary>Upload attempts that reported unusable data.</summary>
    internal int UploadFailureCount => Volatile.Read(ref _uploadFailures);

    /// <summary>Bindings whose destroy call threw; the handle was retired and the reference returned anyway.</summary>
    internal int DestroyFailureCount => Volatile.Read(ref _destroyFailures);

    /// <summary>Pending ids discarded because the asset or its render reference was unavailable.</summary>
    internal int DroppedCount => Volatile.Read(ref _dropped);

    /// <summary>Pending ids the last Drain left for a later frame because of the upload budget.</summary>
    internal int BudgetDeferredCount => Volatile.Read(ref _budgetDeferred);

    /// <summary>Releases bindings, adopts ready ids and uploads pending assets within the per-frame budget.</summary>
    /// <remarks>Render thread only; call once per frame before recording. The first call records the device, and
    /// later calls with a different device throw <see cref="InvalidOperationException"/>. Device contract failures
    /// propagate to the caller and fail the frame.</remarks>
    public void Drain(IRenderDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureDevice(device);

        DrainPinReleased(device);
        DrainReleased(device);
        AdoptReady(device);
        UploadPending(device);
    }

    /// <inheritdoc />
    public bool IsValid(RenderHandle handle)
    {
        if (!handle.IsValid)
            return false;

        lock (_gate)
            return TryGetSlot(handle, out BindingSlot slot) && slot.Binding is not null;
    }

    /// <inheritdoc />
    public bool TryGetBinding<T>(RenderHandle handle, out T binding)
        where T : class, IRuntimeBinding
    {
        lock (_gate)
        {
            if (TryGetSlot(handle, out BindingSlot slot) && slot.Binding is T typed)
            {
                binding = typed;
                return true;
            }
        }

        binding = null!;
        return false;
    }

    /// <inheritdoc />
    public bool TryTakeAdded(out RenderHandle handle) => _added.TryDequeue(out handle);

    /// <inheritdoc />
    public bool TryTakeRemoved(out RenderHandle handle) => _removed.TryDequeue(out handle);

    /// <inheritdoc />
    public int CollectBindings<T>(List<T> destination)
        where T : class, IRuntimeBinding
    {
        ArgumentNullException.ThrowIfNull(destination);

        int collected = 0;
        lock (_gate)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].Binding is T typed)
                {
                    destination.Add(typed);
                    collected++;
                }
            }
        }

        return collected;
    }

    /// <summary>Retires one handle and queues its asset for another upload attempt.</summary>
    /// <returns><c>true</c> when the handle was live; otherwise nothing changes.</returns>
    /// <remarks>Render thread only. The binding is destroyed and a recorded failure is cleared so the next Drain
    /// retries the upload.</remarks>
    public bool Invalidate(RenderHandle handle)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (
            !TryTakeHandle(handle, out AssetId assetId, out IRuntimeBinding binding, out bool owned)
        )
            return false;

        DestroyQuietly(_device!, binding, owned);
        _removed.Enqueue(handle);
        ClearFailed(assetId);

        if (assetId.IsValid)
            AddPending(assetId);

        return true;
    }

    /// <summary>Destroys every binding and the shared fallback texture, then clears the table; later calls do nothing.</summary>
    /// <remarks>Render thread only. A destroy failure is logged and skipped so the remaining bindings still release.
    /// A destroy failure never keeps a handle alive.</remarks>
    public void Dispose(IRenderDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (_disposed)
            return;

        EnsureDevice(device);
        _disposed = true;

        var teardown = new List<(AssetId AssetId, IRuntimeBinding Binding, bool Owned)>();
        lock (_gate)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                BindingSlot slot = _slots[i];
                if (slot.Binding is not { } binding)
                    continue;

                teardown.Add((slot.Asset, binding, slot.Owned));
                slot.Binding = null;
                slot.Owned = false;
                slot.Asset = default;
                slot.Generation = NextGeneration(slot.Generation);
                _freeSlots.Add(i);
                _boundCount--;
            }
        }

        int releasedRefs = 0;
        foreach (var (entryAssetId, entryBinding, entryOwned) in teardown)
        {
            DestroyQuietly(device, entryBinding, entryOwned);

            if (entryOwned && entryAssetId.IsValid && _source.Release(entryAssetId))
                releasedRefs++;
        }

        FallbackTextureBinding? fallback = _fallback;
        _fallback = null;
        if (fallback is not null)
            DestroyQuietly(device, fallback, owned: true);

        int failed;
        lock (_gate)
        {
            failed = _failed.Count;
            _handles.Clear();
            _pending.Clear();
            _failed.Clear();
            _boundCount = 0;
        }

        if (_added.Count > 0)
        {
            while (_added.TryDequeue(out _)) { }
        }
        if (_removed.Count > 0)
        {
            while (_removed.TryDequeue(out _)) { }
        }

        int uploadFailures = Volatile.Read(ref _uploadFailures);
        int destroyFailures = Volatile.Read(ref _destroyFailures);
        int dropped = Volatile.Read(ref _dropped);
        int budgetDeferred = Volatile.Read(ref _budgetDeferred);

        Log.Info(
            $"Render asset binding table disposed: {teardown.Count} binding(s), {releasedRefs} render ref(s), "
                + $"{failed} failed upload(s), {uploadFailures} upload failure attempt(s), "
                + $"{destroyFailures} destroy error(s), {dropped} dropped id(s), "
                + $"{budgetDeferred} budget-deferred id(s)."
        );

#if DEBUG
        System.Diagnostics.Debug.Assert(
            _handles.Count == 0 && _pending.Count == 0 && _boundCount == 0,
            "RenderAssetBindingTable left state behind after Dispose."
        );
#endif
    }

    private void DrainPinReleased(IRenderDevice device)
    {
        while (_source.TryTakePinReleased(out AssetId assetId))
        {
            // A load may revive the retention before this notification is consumed; the binding is valid then.
            if (_source.IsRetentionCount(assetId))
                continue;

            RemovePending(assetId);
            ClearFailed(assetId);

            if (
                !TryRemoveHandle(
                    assetId,
                    out RenderHandle handle,
                    out IRuntimeBinding binding,
                    out bool owned
                )
            )
                continue;

            // The reference is returned even when destroying the device resource fails.
            DestroyQuietly(device, binding, owned);
            _source.Release(assetId);
            _removed.Enqueue(handle);
        }
    }

    private void DrainReleased(IRenderDevice device)
    {
        while (_source.TryTakeReleased(out AssetId assetId))
        {
            RemovePending(assetId);
            ClearFailed(assetId);

            if (
                !TryRemoveHandle(
                    assetId,
                    out RenderHandle handle,
                    out IRuntimeBinding binding,
                    out bool owned
                )
            )
                continue;

            // The store removes a record only after every reference is gone, so nothing is returned here.
            DestroyQuietly(device, binding, owned);
            _removed.Enqueue(handle);
        }
    }

    private void AdoptReady(IRenderDevice device)
    {
        while (_source.TryTakeReady(out AssetId assetId))
        {
            // A revived record re-announces its id: drop a previous fallback and retry the upload.
            if (ClearFailed(assetId))
            {
                RetireFallback(device, assetId);
            }

            if (IsBound(assetId) || IsPending(assetId))
                continue;

            AddPending(assetId);
        }
    }

    private void UploadPending(IRenderDevice device)
    {
        int maxAttempts = Application.AssetSetting.MaxUploadsPerFrame;
        long maxBytes = Application.AssetSetting.MaxUploadBytesPerFrame;
        int attempts = 0;
        long bytes = 0;
        int deferred = 0;

        int index = 0;
        while (index < _pending.Count)
        {
            if (maxAttempts > 0 && attempts >= maxAttempts)
            {
                deferred = _pending.Count - index;
                break;
            }

            AssetId assetId = _pending[index];

            if (!_source.TryGetRenderAsset(assetId, out IAsset? asset))
            {
                // The record left the store between the ready notification and this attempt.
                Interlocked.Increment(ref _dropped);
                RemovePendingAt(index);
                continue;
            }

            if (IsBound(assetId))
            {
                RemovePendingAt(index);
                continue;
            }

            if (!_uploaders.TryGetValue(asset.GetType(), out IAssetUploader? uploader))
            {
                Log.Warning(
                    $"No uploader is registered for asset type '{asset.GetType().Name}'; asset {assetId.Value} stays unbound."
                );
                Interlocked.Increment(ref _dropped);
                RemovePendingAt(index);
                continue;
            }

            long estimate = EstimateUploadBytes(asset);
            if (maxBytes > 0 && bytes + estimate > maxBytes && bytes > 0)
            {
                deferred = _pending.Count - index;
                break;
            }

            if (!_source.TryAcquire<IAsset>(assetId, out _))
            {
                Interlocked.Increment(ref _dropped);
                RemovePendingAt(index);
                continue;
            }

            attempts++;
            bytes += estimate;

            IRuntimeBinding binding;
            AssetUploadResult result;
            var context = new AssetUploadContext(_source, assetId, device);
            try
            {
                result = uploader.TryUpload(in context, out binding);
            }
            catch
            {
                // Device and contract failures fail the frame instead of turning into a silent fallback.
                _source.Release(assetId);
                throw;
            }

            if (result == AssetUploadResult.Uploaded && binding is not null)
            {
                AddBinding(assetId, binding, owned: true);
                RemovePendingAt(index);
                continue;
            }

            _source.Release(assetId);
            RecordUploadFailure(device, assetId, asset, uploader);
            RemovePendingAt(index);
        }

        Volatile.Write(ref _budgetDeferred, deferred);
    }

    private void RecordUploadFailure(
        IRenderDevice device,
        AssetId assetId,
        IAsset asset,
        IAssetUploader uploader
    )
    {
        Interlocked.Increment(ref _uploadFailures);

        string reason = $"{uploader.GetType().Name} could not upload '{asset.GetType().Name}'";
        if (TryMarkFailed(assetId, reason))
        {
            Log.Warning(
                asset is Texture
                    ? $"Asset {assetId.Value} upload failed: {reason}; drawing the fallback texture instead."
                    : $"Asset {assetId.Value} upload failed: {reason}; the asset stays unbound."
            );
        }

        // Only textures have a fallback today; other asset types simply stay unbound.
        if (asset is not Texture || IsBound(assetId))
            return;

        AddBinding(assetId, EnsureFallback(device), owned: false);
    }

    private ITextureBinding EnsureFallback(IRenderDevice device)
    {
        FallbackTextureBinding? fallback = _fallback;
        if (fallback is not null)
            return fallback;

        TextureHandle handle = device.CreateTexture(
            new TextureDesc(1, 1, TextureFormat.RGBA32, TextureColorSpace.SRGB, 1, WhitePixel)
        );

        fallback = new FallbackTextureBinding(handle);
        _fallback = fallback;
        return fallback;
    }

    private void RetireFallback(IRenderDevice device, AssetId assetId)
    {
        if (
            !TryRemoveHandle(
                assetId,
                out RenderHandle handle,
                out IRuntimeBinding binding,
                out bool owned
            )
        )
            return;

        DestroyQuietly(device, binding, owned);
        _removed.Enqueue(handle);
    }

    private RenderHandle AddBinding(AssetId assetId, IRuntimeBinding binding, bool owned)
    {
        RenderHandle handle;
        lock (_gate)
        {
            int index;
            if (_freeSlots.Count > 0)
            {
                index = _freeSlots[^1];
                _freeSlots.RemoveAt(_freeSlots.Count - 1);
            }
            else
            {
                index = _slots.Count;
                _slots.Add(new BindingSlot());
            }

            BindingSlot slot = _slots[index];
            slot.Generation = NextGeneration(slot.Generation);
            slot.Binding = binding;
            slot.Owned = owned;
            slot.Asset = assetId;

            handle = new RenderHandle((uint)index, slot.Generation);
            _handles[assetId] = handle;
            _boundCount++;
        }

        _added.Enqueue(handle);
        return handle;
    }

    private bool TryRemoveHandle(
        AssetId assetId,
        out RenderHandle handle,
        out IRuntimeBinding binding,
        out bool owned
    )
    {
        lock (_gate)
        {
            if (!_handles.Remove(assetId, out handle) || !TryGetSlot(handle, out BindingSlot slot))
            {
                binding = null!;
                owned = false;
                return false;
            }

            binding = slot.Binding!;
            owned = slot.Owned;
            RetireSlot(handle.Slot, slot);
            return true;
        }
    }

    private bool TryTakeHandle(
        RenderHandle handle,
        out AssetId assetId,
        out IRuntimeBinding binding,
        out bool owned
    )
    {
        lock (_gate)
        {
            if (!TryGetSlot(handle, out BindingSlot slot))
            {
                assetId = default;
                binding = null!;
                owned = false;
                return false;
            }

            assetId = slot.Asset;
            binding = slot.Binding!;
            owned = slot.Owned;
            _handles.Remove(assetId);
            RetireSlot(handle.Slot, slot);
            return true;
        }
    }

    private void RetireSlot(uint index, BindingSlot slot)
    {
        slot.Binding = null;
        slot.Owned = false;
        slot.Asset = default;
        slot.Generation = NextGeneration(slot.Generation);
        _freeSlots.Add((int)index);
        _boundCount--;
    }

    private bool TryGetSlot(RenderHandle handle, out BindingSlot slot)
    {
        slot = null!;
        if (!handle.IsValid)
            return false;

        int index = (int)handle.Slot;
        if (index < 0 || index >= _slots.Count)
            return false;

        BindingSlot candidate = _slots[index];
        if (candidate.Generation != handle.Generation || candidate.Binding is null)
            return false;

        slot = candidate;
        return true;
    }

    private static uint NextGeneration(uint generation) =>
        generation == uint.MaxValue ? 1u : generation + 1u;

    private void DestroyQuietly(IRenderDevice device, IRuntimeBinding binding, bool owned)
    {
        if (!owned)
            return;

        try
        {
            binding.Destroy(device);
        }
        catch (Exception error)
        {
            // A failing destroy must not keep the handle or the render reference alive.
            Interlocked.Increment(ref _destroyFailures);
            Log.Exception(error);
        }
    }

    /// 这里用于估计上传的byte数量，后续增加内容的时候直接改switch就行，这里必须是hardcode来处理各种实际情况
    private static long EstimateUploadBytes(IAsset asset) =>
        asset switch
        {
            Texture texture => (long)texture.Width * texture.Height * Rgba32BytesPerPixel,
            _ => 0L,
        };

    private void EnsureDevice(IRenderDevice device)
    {
        IRenderDevice? recorded = _device;
        if (recorded is null)
        {
            _device = device;
            return;
        }

        if (!ReferenceEquals(recorded, device))
            throw new InvalidOperationException(
                "RenderAssetBindingTable records the render device on its first call and cannot switch devices."
            );
    }

    private bool IsBound(AssetId assetId)
    {
        lock (_gate)
            return _handles.ContainsKey(assetId);
    }

    private bool IsPending(AssetId assetId) => _pending.Contains(assetId);

    private void AddPending(AssetId assetId)
    {
        lock (_gate)
        {
            if (!_pending.Contains(assetId))
                _pending.Add(assetId);
        }
    }

    private void RemovePending(AssetId assetId)
    {
        lock (_gate)
            _pending.Remove(assetId);
    }

    private void RemovePendingAt(int index)
    {
        lock (_gate)
            _pending.RemoveAt(index);
    }

    private bool TryMarkFailed(AssetId assetId, string reason)
    {
        lock (_gate)
            return _failed.TryAdd(assetId, reason);
    }

    private bool ClearFailed(AssetId assetId)
    {
        lock (_gate)
            return _failed.Remove(assetId);
    }

    private sealed class BindingSlot
    {
        internal uint Generation;
        internal IRuntimeBinding? Binding;
        internal bool Owned;
        internal AssetId Asset;
    }
}
