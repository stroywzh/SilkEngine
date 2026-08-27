namespace SilkEngine.Assets.Serialization;

/// <summary>
/// SQL 序列化记录存储桩：构造与方法均不访问数据库；所有方法直接抛 <see cref="NotImplementedException"/>。
/// 待引入数据库层后实现真实持久化。
/// </summary>
public sealed class SqlAssetSerializerStore : IAssetSerializerStore
{
    /// <summary>构造存储桩；不访问数据库</summary>
    public SqlAssetSerializerStore()
    {
    }

    /// <summary>保存记录；桩实现直接抛 <see cref="NotImplementedException"/></summary>
    /// <param name="record">待保存记录</param>
    /// <returns>永不返回（抛异常）</returns>
    public Task SaveAsync(AssetSerializationRecord record)
        => throw new NotImplementedException("SqlAssetSerializerStore 尚未实现保存");

    /// <summary>读取记录；桩实现直接抛 <see cref="NotImplementedException"/></summary>
    /// <param name="assetId">资产 ID</param>
    /// <returns>永不返回（抛异常）</returns>
    public Task<AssetSerializationRecord?> LoadAsync(AssetId assetId)
        => throw new NotImplementedException("SqlAssetSerializerStore 尚未实现读取");
}
