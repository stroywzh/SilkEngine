using SilkEngine.Core.AssetSystem;

namespace SilkEngine.Asset;

public abstract class VirtualAssetBase
{
    private int _refCount = 0;
    internal AssetHandle Handle { get; set; }
    internal int RefCount => _refCount;

    internal void LoadAsset()
    {
        if (_refCount > 0)
        {
            _refCount += 1;
            return;
        }

        try
        {
            OnLoad();
            _refCount += 1;
        }
        catch (Exception ex)
        {
            Log.Exception(ex);
        }
    }

    internal void ReleaseAsset()
    {
        if (_refCount > 1)
        {
            _refCount -= 1;
            return;
        }

        try
        {
            OnRelease();
        }
        catch (Exception ex)
        {
            Log.Exception(ex);
        }
        finally
        {
            _refCount -= 1;
        }
    }

    protected abstract void OnLoad();

    protected abstract void OnRelease();
}
