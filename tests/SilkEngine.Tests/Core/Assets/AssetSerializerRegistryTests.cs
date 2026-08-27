using SilkEngine.Assets;
using SilkEngine.Assets.Serialization;

namespace SilkEngine.Tests.Core.Assets;

/// <summary>序列化器注册表与存储测试：类型注册互斥、版本解析、内存存储往返与 SQL 桩行为</summary>
public class AssetSerializerRegistryTests
{
    [Fact]
    public void Registry_RejectsDuplicateTypeAndUnsupportedVersion()
    {
        var registry = new AssetSerializerRegistry();
        registry.Register(new TestSerializer(new AssetTypeId("mesh"), 1, 1));

        Assert.Throws<InvalidOperationException>(() => registry.Register(
            new TestSerializer(new AssetTypeId("mesh"), 1, 1)));
        Assert.Throws<NotSupportedException>(() => registry.Resolve(
            new AssetTypeId("mesh"), schemaVersion: 2));
    }

    [Fact]
    public void Registry_InstancesDoNotShareState()
    {
        var a = new AssetSerializerRegistry();
        var b = new AssetSerializerRegistry();
        a.Register(new TestSerializer(new AssetTypeId("mesh"), 1, 1));

        Assert.Throws<NotSupportedException>(() => b.Resolve(new AssetTypeId("mesh"), 1));
    }

    [Fact]
    public void Registry_NullSerializerOrEmptyTypeId_ThrowsArgumentException()
    {
        var registry = new AssetSerializerRegistry();

        Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
        Assert.Throws<ArgumentException>(() => registry.Register(
            new TestSerializer(default, 1, 1)));
        Assert.Throws<ArgumentException>(() => registry.Register(
            new TestSerializer(new AssetTypeId(""), 1, 1)));
    }

    [Fact]
    public void Registry_UnknownType_ThrowsNotSupportedException()
    {
        var registry = new AssetSerializerRegistry();

        Assert.Throws<NotSupportedException>(() => registry.Resolve(new AssetTypeId("unknown"), 1));
    }

    [Fact]
    public async Task InMemoryStore_RoundTripsRecord()
    {
        var store = new InMemoryAssetSerializerStore();
        var record = Fixtures.SerializationRecord();

        await store.SaveAsync(record);
        var loaded = await store.LoadAsync(record.AssetId);

        Assert.Equal(record, loaded);
    }

    [Fact]
    public async Task InMemoryStore_LoadMiss_ReturnsNull()
    {
        var store = new InMemoryAssetSerializerStore();

        var loaded = await store.LoadAsync(new AssetId(Guid.NewGuid()));

        Assert.Null(loaded);
    }

    [Fact]
    public async Task InMemoryStore_DuplicateAssetId_OverwritesWithLatest()
    {
        var store = new InMemoryAssetSerializerStore();
        var id = new AssetId(Guid.NewGuid());
        var first = Fixtures.SerializationRecord(assetId: id);
        var second = Fixtures.SerializationRecord(assetId: id);

        await store.SaveAsync(first);
        await store.SaveAsync(second);

        var loaded = await store.LoadAsync(id);
        Assert.Equal(second, loaded);
    }

    [Fact]
    public async Task SqlStore_ThrowsNotImplementedException()
    {
        var store = new SqlAssetSerializerStore();

        await Assert.ThrowsAsync<NotImplementedException>(() =>
            store.SaveAsync(Fixtures.SerializationRecord()));
        await Assert.ThrowsAsync<NotImplementedException>(() =>
            store.LoadAsync(new AssetId(Guid.NewGuid())));
    }
}
