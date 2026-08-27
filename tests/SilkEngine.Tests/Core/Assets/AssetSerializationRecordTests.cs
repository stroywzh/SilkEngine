using SilkEngine.Assets;
using SilkEngine.Assets.Serialization;

namespace SilkEngine.Tests.Core.Assets;

/// <summary>资产序列化记录与序列化器契约测试：身份/版本/依赖载体、版本范围声明与参数校验</summary>
public class AssetSerializationRecordTests
{
    [Fact]
    public void Record_ContainsIdentityVersionAndDependencies()
    {
        var record = new AssetSerializationRecord
        {
            SchemaVersion = 1,
            TypeId = new AssetTypeId("material"),
            AssetId = new AssetId(Guid.NewGuid()),
            SourceNodeId = new VirtualNodeId(Guid.NewGuid()),
            Dependencies = [new UntypedAssetHandle(
                new AssetId(Guid.NewGuid()), new AssetTypeId("texture"))],
            Data = "{}"
        };

        Assert.Equal(1, record.SchemaVersion);
        Assert.Single(record.Dependencies);
        Assert.Equal("material", record.TypeId.Value);
    }

    [Fact]
    public void SerializerContract_ReportsSupportedTypeAndVersions()
    {
        IAssetSerializer serializer = new TestSerializer(
            new AssetTypeId("test"), minVersion: 1, maxVersion: 2);

        Assert.Equal(new AssetTypeId("test"), serializer.TypeId);
        Assert.True(serializer.SupportsVersion(2));
        Assert.False(serializer.SupportsVersion(3));
    }

    [Fact]
    public void Record_NullOrEmptyTypeId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new AssetSerializationRecord { TypeId = default });
        Assert.Throws<ArgumentException>(() => new AssetSerializationRecord { TypeId = new AssetTypeId("") });
    }

    [Fact]
    public void Record_NegativeSchemaVersion_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AssetSerializationRecord { SchemaVersion = -1 });
    }

    [Fact]
    public void Record_NullData_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new AssetSerializationRecord { Data = null! });
    }

    [Fact]
    public void Record_DependenciesAreCopiedOnWrite()
    {
        List<UntypedAssetHandle> deps = [new UntypedAssetHandle(new AssetId(Guid.NewGuid()))];
        var record = new AssetSerializationRecord { Dependencies = deps };

        deps.Clear();

        Assert.Single(record.Dependencies);
    }

    [Fact]
    public void Record_EqualityComparesContentIncludingDependencies()
    {
        var id = new AssetId(Guid.NewGuid());
        var dep = new UntypedAssetHandle(new AssetId(Guid.NewGuid()), new AssetTypeId("texture"));
        var a = new AssetSerializationRecord
        {
            SchemaVersion = 1,
            TypeId = new AssetTypeId("material"),
            AssetId = id,
            SourceNodeId = new VirtualNodeId(Guid.NewGuid()),
            Dependencies = [dep],
            Data = "{}"
        };
        var b = new AssetSerializationRecord
        {
            SchemaVersion = 1,
            TypeId = new AssetTypeId("material"),
            AssetId = id,
            SourceNodeId = a.SourceNodeId,
            Dependencies = [dep],
            Data = "{}"
        };

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Record_EqualityIsSensitiveToDependencySequence()
    {
        var id = new AssetId(Guid.NewGuid());
        var depA = new UntypedAssetHandle(new AssetId(Guid.NewGuid()), new AssetTypeId("texture"));
        var depB = new UntypedAssetHandle(new AssetId(Guid.NewGuid()), new AssetTypeId("shader"));
        var original = new AssetSerializationRecord
        {
            SchemaVersion = 1,
            TypeId = new AssetTypeId("material"),
            AssetId = id,
            Dependencies = [depA, depB],
            Data = "{}"
        };
        var swapped = original with { Dependencies = [depB, depA] };

        Assert.NotEqual(original, swapped);
    }
}
