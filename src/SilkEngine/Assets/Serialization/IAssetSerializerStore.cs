namespace SilkEngine.Assets.Serialization;

/// <summary>
/// 序列化记录存储：按资产 ID 保存/读取序列化记录。
/// 实现负责数据持久化，不得改变记录内容；未命中约定由实现文档说明（返回 null 或抛 <see cref="System.Collections.Generic.KeyNotFoundException"/>）。
/// </summary>
public interface IAssetSerializerStore
{
    /// <summary>保存序列化记录（同步完成）</summary>
    /// <param name="record">待保存记录</param>
    Task SaveAsync(AssetSerializationRecord record);

    /// <summary>按资产 ID 读取序列化记录</summary>
    /// <param name="assetId">资产 ID</param>
    /// <returns>序列化记录；未命中返回 null</returns>
    Task<AssetSerializationRecord?> LoadAsync(AssetId assetId);
}
