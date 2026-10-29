using SilkEngine.Asset;

namespace SilkEngine.Core.AssetSystem;

/// <summary>Entry point of the asset system.</summary>
/// <remarks>预留：未实现/未接线；计划批次见 proposal-04 D-A5（资产系统立项）。</remarks>
internal class AssetSystem : ManagerBase<AssetSystem>
{
    private ulong _currentId = 0;
    private ulong _nextId = 1;
    public T Load<T>(string path)
        where T : VirtualAssetBase
    {
        throw new NotImplementedException();
    }

    private AssetHandle GetAssetHandle()
    {
        return AssetHandle.Invalid;
    }
}
