using SilkEngine.Asset;

namespace SilkEngine.Core.AssetSystem;

internal static class AssetTable
{
    private static readonly Dictionary<Type, Dictionary<AssetHandle, VirtualAssetBase>> _assetsMap =
        new() { [typeof(Texture)] = new() };

    public static bool TryRegister(VirtualAssetBase asset)
    {
        var handle = asset.Handle;
        if (!handle.IsValid)
            return false;

        var type = asset.GetType();
        if (!_assetsMap.TryGetValue(type, out var handleMap))
        {
            handleMap = new Dictionary<AssetHandle, VirtualAssetBase>();
            _assetsMap[type] = handleMap;
        }

        handleMap[handle] = asset;
        return true;
    }

    public static bool TryRemove(VirtualAssetBase asset)
    {
        var type = asset.GetType();
        return _assetsMap.TryGetValue(type, out var handleMap) && handleMap.Remove(asset.Handle);
    }

    // public static bool TryRemove<T>(T asset)
    // where T : VirtualAssetBase

    // public static bool TryRemove<T>(T asset)
    //     where T : VirtualAssetBase
    // {
    //     var type = typeof(T);
    //     if (_assetsMap.TryGetValue(type, out var handleMap))
    //     {
    //         if (handleMap.ContainsValue(asset))
    //         {
    //             //这里怎么干掉这个Handle？
    //             return true;
    //         }
    //     }
    //     return false;
    // }
}
