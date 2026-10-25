namespace SilkEngine.Core.AssetSystem;

/// <summary>
/// Handle to an asset system entry, expressed as a generic 64-bit resource id.
/// </summary>
/// <remarks>预留：未实现/未接线；计划批次见 proposal-04 D-A5（资产系统立项）。</remarks>
public readonly struct AssetHandle
{
    public readonly Handle<ulong> AssetId;
}
