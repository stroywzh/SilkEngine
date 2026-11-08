using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;
using SilkEngine.Core.AssetSystem;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.Assets;

/// <summary>Render-thread cache of device bindings for loaded assets; owns and releases every binding it creates.</summary>
/// <remarks>The engine registers its default uploaders (texture today) in the constructor. Upload failures never
/// escape into the render loop: retryable failures are bounded by attempts/time, permanent failures leave the
/// pending set immediately.</remarks>
public sealed class AssetRuntimeCache
{
    private const int MaxUploadAttempts = 3;
    private static readonly TimeSpan RetryWindow = TimeSpan.FromSeconds(5);

    private readonly List<IAssetUploader> _uploaders = new();
    private readonly Dictionary<AssetId, IRuntimeBinding> _bindings = new();
    private readonly List<AssetId> _order = new();
    private readonly List<AssetId> _pending = new();
    private readonly Dictionary<AssetId, PendingUpload> _pendingInfo = new();

    public AssetRuntimeCache()
    {
        // Engine default runtime assets own their uploaders; business pipelines do not register them.
        _uploaders.Add(new TextureUploader());
    }

    /// <summary>Ids that are currently bound, in bind order; readable on the render thread.</summary>
    public IReadOnlyList<AssetId> Order => _order;

    /// <summary>Registers an additional uploader ahead of the defaults (engine extensions and tests).</summary>
    internal void AddUploader(IAssetUploader uploader)
    {
        ArgumentNullException.ThrowIfNull(uploader);
        _uploaders.Insert(0, uploader);
    }

    /// <summary>Releases bindings first, then adopts ready ids, then retries pending uploads; failed uploads stay pending.</summary>
    /// <remarks>Render thread only; call once per frame before reading <see cref="Order"/>.</remarks>
    public void Drain(IRenderDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        // A pin release does not remove the record while this cache still holds a render reference; the
        // notification lets the cache destroy the binding and release that reference first.
        while (AssetRenderBridge.TryTakePinReleased(out AssetId pinReleased))
        {
            // A load may revive the pin before this notification is consumed; the binding is still valid then.
            if (AssetRenderBridge.IsPinHeld(pinReleased))
                continue;

            RemovePending(pinReleased);

            if (!_bindings.Remove(pinReleased, out IRuntimeBinding? binding))
                continue;

            binding.Destroy(device);
            _order.Remove(pinReleased);
            AssetRenderBridge.Release(pinReleased);
        }

        while (AssetRenderBridge.TryTakeReleased(out AssetId released))
        {
            RemovePending(released);

            if (!_bindings.Remove(released, out IRuntimeBinding? binding))
                continue;

            // The store only removes a record after every reference is gone, so there is nothing left to
            // release here; the binding is destroyed to keep the device side in sync.
            binding.Destroy(device);
            _order.Remove(released);
        }

        while (AssetRenderBridge.TryTakeReady(out AssetId ready))
        {
            if (!_pending.Contains(ready) && !_bindings.ContainsKey(ready))
            {
                _pending.Add(ready);
                _pendingInfo[ready] = new PendingUpload(Environment.TickCount64);
            }
        }

        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            AssetId assetId = _pending[i];

            if (
                !AssetRenderBridge.TryGetRenderAsset(assetId, out IAsset? asset)
                || _bindings.ContainsKey(assetId)
            )
            {
                RemovePending(assetId);
                continue;
            }

            IAssetUploader? uploader = FindUploader(asset);
            if (uploader is null)
            {
                RemovePending(assetId);
                continue;
            }

            // Hold a render reference for the binding lifetime; released when the binding is destroyed.
            if (!AssetRenderBridge.TryAcquire<IAsset>(assetId, out _))
            {
                RemovePending(assetId);
                continue;
            }

            AssetUploadResult result;
            IRuntimeBinding? binding;
            try
            {
                result = uploader.TryUpload(device, assetId, out binding);
            }
            catch (Exception error)
            {
                // Uploaders are not allowed to throw into the render loop; treat an escaping exception as permanent.
                Log.Exception(error);
                result = AssetUploadResult.PermanentFailure;
                binding = null;
            }

            if (result == AssetUploadResult.Uploaded && binding is not null)
            {
                _bindings.Add(assetId, binding);
                _order.Add(assetId);
                RemovePending(assetId);
                continue;
            }

            AssetRenderBridge.Release(assetId);

            if (result == AssetUploadResult.RetryableFailure && TryRecordRetry(assetId))
                continue;

            if (result == AssetUploadResult.PermanentFailure)
            {
                Log.Warning(
                    $"Asset {assetId.Value} upload failed permanently; the render side will not retry."
                );
            }

            RemovePending(assetId);
        }
    }

    /// <summary>Returns the binding cached for the asset id when it exists and has the requested type.</summary>
    /// <remarks>Render thread only.</remarks>
    public bool TryGet<T>(AssetId assetId, out T binding)
        where T : class, IRuntimeBinding
    {
        if (_bindings.TryGetValue(assetId, out IRuntimeBinding? found) && found is T typed)
        {
            binding = typed;
            return true;
        }

        binding = null!;
        return false;
    }

    /// <summary>Destroys every binding and clears the cache; calling this more than once is a no-op.</summary>
    /// <remarks>Render thread only.</remarks>
    public void Dispose(IRenderDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        foreach (IRuntimeBinding binding in _bindings.Values)
            binding.Destroy(device);

        foreach (AssetId assetId in _bindings.Keys)
            AssetRenderBridge.Release(assetId);

        _bindings.Clear();
        _order.Clear();
        _pending.Clear();
        _pendingInfo.Clear();
    }

    private bool TryRecordRetry(AssetId assetId)
    {
        if (!_pendingInfo.TryGetValue(assetId, out PendingUpload? info))
        {
            info = new PendingUpload(Environment.TickCount64);
            _pendingInfo[assetId] = info;
        }

        info.Attempts++;
        if (
            info.Attempts >= MaxUploadAttempts
            || Environment.TickCount64 - info.FirstAttemptMs >= (long)RetryWindow.TotalMilliseconds
        )
        {
            Log.Warning(
                $"Asset {assetId.Value} upload did not become available after {info.Attempts} attempt(s); giving up."
            );
            return false;
        }

        return true;
    }

    private IAssetUploader? FindUploader(IAsset asset)
    {
        foreach (IAssetUploader uploader in _uploaders)
        {
            if (uploader.AssetType.IsInstanceOfType(asset))
                return uploader;
        }

        return null;
    }

    private void RemovePending(AssetId assetId)
    {
        _pending.Remove(assetId);
        _pendingInfo.Remove(assetId);
    }

    private sealed class PendingUpload
    {
        internal PendingUpload(long firstAttemptMs) => FirstAttemptMs = firstAttemptMs;

        internal int Attempts { get; set; }

        internal long FirstAttemptMs { get; }
    }
}
