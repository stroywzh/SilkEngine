using SilkEngine.Assets;
using SilkEngine.Assets.Serialization;
using SilkEngine.Render;

namespace SilkEngine.Tests.Core.Assets;

/// <summary>资产反序列化服务测试：依赖先解析后发布、缺失依赖不发布半成品、循环依赖拒绝与幂等</summary>
public class AssetSerializationTests
{
    [Fact]
    public void Deserialize_ResolvesDependenciesBeforePublishingAsset()
    {
        var records = Fixtures.MaterialGraphRecords();
        var resolver = new RecordingReferenceResolver(records.Material, records.Shader, records.Texture);
        var service = Fixtures.SerializationService(resolver);

        var result = service.Deserialize(records.Material.AssetId);

        Assert.True(result.IsSuccess);
        Assert.Equal(records.Material.Dependencies.Select(d => d.Id), resolver.ResolvedIds);
        Assert.True(service.Contains(records.Material.AssetId));
    }

    [Fact]
    public void Deserialize_MissingDependencyDoesNotPublishPartialAsset()
    {
        var resolver = new MissingReferenceResolver();
        var service = Fixtures.SerializationService(resolver);

        Assert.Throws<KeyNotFoundException>(() =>
            service.Deserialize(Fixtures.MaterialAssetId));
        Assert.False(service.Contains(Fixtures.MaterialAssetId));
    }

    [Fact]
    public void Deserialize_CyclicDependenciesAreRejected()
    {
        var service = Fixtures.SerializationService(
            new RecordingReferenceResolver(Fixtures.CyclicGraphRecords()));

        Assert.Throws<InvalidDataException>(() =>
            service.Deserialize(Fixtures.CyclicAssetId));
        Assert.False(service.Contains(Fixtures.CyclicAssetId));
    }

    [Fact]
    public void Deserialize_RepeatedCallIsIdempotent()
    {
        var records = Fixtures.MaterialGraphRecords();
        var resolver = new RecordingReferenceResolver(records.Material, records.Shader, records.Texture);
        var service = Fixtures.SerializationService(resolver);

        service.Deserialize(records.Material.AssetId);
        var resolvedAfterFirst = resolver.ResolvedIds.Count;
        service.Deserialize(records.Material.AssetId);

        Assert.Equal(resolvedAfterFirst, resolver.ResolvedIds.Count);
        Assert.True(service.Contains(records.Material.AssetId));
    }

    [Fact]
    public void Deserialize_SharedDependencyIsResolvedPerEdgeAndPublishedOnce()
    {
        var records = Fixtures.MaterialGraphRecords();
        var secondMaterial = new MaterialAssetSerializer().Serialize(new MaterialAsset(
            new AssetId(Guid.NewGuid()),
            new AssetHandle<ShaderAsset>(records.Shader.AssetId),
            null,
            new MaterialParameterSnapshot([])));
        var resolver = new RecordingReferenceResolver(
            records.Material, records.Shader, records.Texture, secondMaterial);
        var service = Fixtures.SerializationService(resolver);

        Assert.True(service.Deserialize(records.Material.AssetId).IsSuccess);
        Assert.True(service.Deserialize(secondMaterial.AssetId).IsSuccess);

        Assert.Equal(
            records.Material.Dependencies.Count + secondMaterial.Dependencies.Count,
            resolver.ResolvedIds.Count);
        Assert.Equal(2, resolver.ResolvedIds.Count(id => id == records.Shader.AssetId));
    }

    [Fact]
    public void Deserialize_UnknownAssetId_ThrowsKeyNotFoundException()
    {
        var records = Fixtures.MaterialGraphRecords();
        var service = Fixtures.SerializationService(
            new RecordingReferenceResolver(records.Material, records.Shader, records.Texture));

        Assert.Throws<KeyNotFoundException>(() =>
            service.Deserialize(new AssetId(Guid.NewGuid())));
    }

    [Fact]
    public void Deserialize_UnknownSerializer_ThrowsNotSupportedException()
    {
        var record = Fixtures.SerializationRecord(type: "audio");
        var service = Fixtures.SerializationService(new RecordingReferenceResolver(record));

        Assert.Throws<NotSupportedException>(() => service.Deserialize(record.AssetId));
        Assert.False(service.Contains(record.AssetId));
    }

    [Fact]
    public void Deserialize_IncompatibleSchemaVersion_ThrowsNotSupportedException()
    {
        var record = Fixtures.SerializationRecord(type: "texture", version: 99);
        var service = Fixtures.SerializationService(new RecordingReferenceResolver(record));

        Assert.Throws<NotSupportedException>(() => service.Deserialize(record.AssetId));
        Assert.False(service.Contains(record.AssetId));
    }

    [Fact]
    public void Deserialize_CorruptData_ThrowsInvalidDataExceptionAndPublishesNothing()
    {
        var record = Fixtures.SerializationRecord(type: "shader") with { Data = "not-json" };
        var service = Fixtures.SerializationService(new RecordingReferenceResolver(record));

        Assert.Throws<InvalidDataException>(() => service.Deserialize(record.AssetId));
        Assert.False(service.Contains(record.AssetId));
    }
}
