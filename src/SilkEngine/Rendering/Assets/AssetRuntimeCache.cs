using SilkEngine.Asset;
using SilkEngine.Asset.Abstractions;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.Assets;

/// <summary>Render-thread cache of device bindings for loaded assets; owns and releases every binding it creates.</summary>
public sealed class AssetRuntimeCache
{
    private readonly List<IAssetUploader> _uploaders = new();
    private readonly Dictionary<AssetId, IRuntimeBinding> _bindings = new();
    private readonly List<AssetId> _order = new();
    private readonly List<AssetId> _pending = new();

    /// <summary>Ids that are currently bound, in bind order; readable on the render thread.</summary>
    public IReadOnlyList<AssetId> Order => _order;

    /// <summary>Registers an uploader consulted for matching asset types.</summary>
    /// <remarks>Call on the render thread before the first <see cref="Drain"/>.</remarks>
    public void AddUploader(IAssetUploader uploader)
    {
        ArgumentNullException.ThrowIfNull(uploader);
        _uploaders.Add(uploader);
    }

    /// <summary>Releases bindings first, then adopts ready ids, then retries pending uploads; failed uploads stay pending.</summary>
    /// <remarks>Render thread only; call once per frame before reading <see cref="Order"/>.</remarks>
    public void Drain(IRenderDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        // The SilkEngine.Rendering.Assets namespace shadows the Assets facade, so qualify it here.
        while (SilkEngine.Assets.TryTakeReleased(out AssetId released))
        {
            _pending.Remove(released);

            if (!_bindings.Remove(released, out IRuntimeBinding? binding))
                continue;

            binding.Destroy(device);
            _order.Remove(released);
        }

        while (SilkEngine.Assets.TryTakeReady(out AssetId ready))
        {
            if (!_pending.Contains(ready) && !_bindings.ContainsKey(ready))
            {
                _pending.Add(ready);
            }
        }

        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            AssetId assetId = _pending[i];

            if (
                !SilkEngine.Assets.TryGetRenderAsset(assetId, out IAsset? asset)
                || _bindings.ContainsKey(assetId)
            )
            {
                _pending.RemoveAt(i);
                continue;
            }

            IAssetUploader? uploader = FindUploader(asset);
            if (uploader is null)
            {
                _pending.RemoveAt(i);
                continue;
            }

            if (!uploader.TryUpload(device, assetId, out IRuntimeBinding? binding))
                continue;

            _bindings.Add(assetId, binding);
            _order.Add(assetId);
            _pending.RemoveAt(i);
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
        {
            binding.Destroy(device);
        }

        _bindings.Clear();
        _order.Clear();
        _pending.Clear();
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
}
