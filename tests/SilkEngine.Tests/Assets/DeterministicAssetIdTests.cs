using System.Security.Cryptography;
using System.Text;
using SilkEngine.Assets;
using SilkEngine.Assets.Database;
using SilkEngine.Assets.VirtualFileSystem;

namespace SilkEngine.Tests.Assets;

/// <summary>确定性身份测试集合：SQLite/加密等重初始化禁用并行，避免进程级分配噪声干扰零分配测量类测试</summary>
[CollectionDefinition("DeterministicAssetId", DisableParallelization = true)]
public sealed class DeterministicAssetIdCollection
{
}

/// <summary>
/// 确定性资产身份测试：AssetIdFactory 跨实例稳定、路径/类型为身份输入、磁盘扫描携带内容指纹、
/// 目录确定性 ID 与瞬态随机 ID 分离、AssetDB 按规范化路径对账 FileNodes 与 Assets。
/// </summary>
[Collection("DeterministicAssetId")]
public class DeterministicAssetIdTests
{
    [Fact]
    public void SameProjectPathAndType_ProducesSameAssetIdAcrossCatalogInstances()
    {
        var first = AssetIdFactory.Create("sandbox", "Materials/Cube.asset", new AssetTypeId("material"));
        var second = AssetIdFactory.Create("sandbox", "Materials/Cube.asset", new AssetTypeId("material"));

        Assert.Equal(first, second);
    }

    [Fact]
    public void PathAndTypeAreIdentityInputs()
    {
        var path = AssetIdFactory.Create("sandbox", "Meshes/Cube.obj", new AssetTypeId("mesh"));
        var differentPath = AssetIdFactory.Create("sandbox", "Meshes/Other.obj", new AssetTypeId("mesh"));
        var differentType = AssetIdFactory.Create("sandbox", "Meshes/Cube.obj", new AssetTypeId("texture"));

        Assert.NotEqual(path, differentPath);
        Assert.NotEqual(path, differentType);
    }

    [Fact]
    public void DiskScan_RecordsContentFingerprint()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(root, "a.hlsl"), "float4 frag() : SV_Target { return 1; }");
        var scan = new DiskAssetFileSystem(root).Scan();

        var file = Assert.Single(scan.Files.Where(x => x.LogicalPath == "a.hlsl"));
        Assert.NotNull(file.SourceFingerprint);
        Assert.NotEmpty(file.SourceFingerprint!);
    }

    [Fact]
    public void Factory_PathNormalizationIsCanonical()
    {
        var expected = AssetIdFactory.Create("sandbox", "Materials/Cube.asset", new AssetTypeId("material"));

        Assert.Equal(expected, AssetIdFactory.Create("sandbox", "Materials\\Cube.asset", new AssetTypeId("material")));
        Assert.Equal(expected, AssetIdFactory.Create("sandbox", "/Materials/Cube.asset/", new AssetTypeId("material")));
        Assert.Equal(expected, AssetIdFactory.Create("sandbox", "Materials//Cube.asset", new AssetTypeId("material")));
        Assert.NotEqual(expected, AssetIdFactory.Create("sandbox", "materials/cube.asset", new AssetTypeId("material")));
        Assert.NotEqual(expected, AssetIdFactory.Create("other", "Materials/Cube.asset", new AssetTypeId("material")));
    }

    [Fact]
    public void Factory_SetsRfc4122VersionAndVariantBits()
    {
        var id = AssetIdFactory.Create("sandbox", "a.hlsl", new AssetTypeId("shader"));

        var bytes = id.Value.ToByteArray();
        Assert.Equal(5, bytes[7] >> 4);
        Assert.Equal(0x80, bytes[8] & 0xC0);
    }

    [Fact]
    public void Catalog_DiskMode_SamePathAndType_YieldsSameIdAcrossInstances()
    {
        var root = TestTempDirectory.Create();
        try
        {
            File.WriteAllText(Path.Combine(root, "cube.material"), "material");
            var scan = new DiskAssetFileSystem(root).Scan();

            var firstIndex = new InMemoryVirtualFileIndex();
            firstIndex.Apply(scan);
            var secondIndex = new InMemoryVirtualFileIndex();
            secondIndex.Apply(scan);
            Assert.True(firstIndex.TryGet("cube.material", out var node));
            Assert.True(secondIndex.TryGet("cube.material", out var secondNode));

            var first = new AssetCatalog("sandbox", firstIndex)
                .GetOrAdd(node!.Id, new AssetTypeId("material"));
            var second = new AssetCatalog("sandbox", secondIndex)
                .GetOrAdd(secondNode!.Id, new AssetTypeId("material"));

            Assert.Equal(first.AssetId, second.AssetId);
            Assert.Equal(
                AssetIdFactory.Create("sandbox", "cube.material", new AssetTypeId("material")),
                first.AssetId);
        }
        finally
        {
            TestTempDirectory.Delete(root);
        }
    }

    [Fact]
    public void Catalog_TransientMode_KeepsRandomIds()
    {
        var nodeId = new VirtualNodeId(Guid.NewGuid());

        var first = new AssetCatalog().GetOrAdd(nodeId, new AssetTypeId("material"));
        var second = new AssetCatalog().GetOrAdd(nodeId, new AssetTypeId("material"));

        Assert.NotEqual(first.AssetId, second.AssetId);
    }

    [Fact]
    public async Task Catalog_WithDatabase_ReconcilesFileNodeAndAsset()
    {
        var root = TestTempDirectory.Create();
        try
        {
            const string content = "float4 main() : SV_Target { return 0; }";
            await File.WriteAllTextAsync(Path.Combine(root, "cube.hlsl"), content);
            var files = new DiskAssetFileSystem(root);
            var index = new InMemoryVirtualFileIndex();
            index.Apply(files.Scan());
            Assert.True(index.TryGet("cube.hlsl", out var node));
            Assert.NotNull(node!.MetaData?.SourceFingerprint);

            await using var db = new SqliteAssetDatabase(Path.Combine(root, "assets.db"));
            await db.InitializeAsync(CancellationToken.None);
            var catalog = new AssetCatalog("sandbox", index, db);

            var record = catalog.GetOrAdd(node.Id, new AssetTypeId("shader"));

            var snapshot = await db.CaptureSnapshotAsync(CancellationToken.None);
            var fileNode = Assert.Single(snapshot.FileNodes);
            Assert.Equal(node.Id, fileNode.NodeId);
            Assert.Equal("cube.hlsl", fileNode.LogicalPath);
            var asset = Assert.Single(snapshot.Assets);
            Assert.Equal(record.AssetId, asset.AssetId);
            Assert.Equal("cube.hlsl", asset.LogicalPath);
            Assert.Equal("shader", asset.AssetType.Value);
            Assert.Equal(node.MetaData!.SourceFingerprint, asset.SourceFingerprint);
            Assert.Equal(0UL, asset.SourceRevision);
        }
        finally
        {
            TestTempDirectory.Delete(root);
        }
    }

    [Fact]
    public async Task Catalog_Reconcile_ReplacesStaleAssetRowWithSameLogicalPath()
    {
        var root = TestTempDirectory.Create();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "cube.hlsl"), "float4 main() { return 0; }");
            var files = new DiskAssetFileSystem(root);
            var index = new InMemoryVirtualFileIndex();
            index.Apply(files.Scan());
            Assert.True(index.TryGet("cube.hlsl", out var node));

            var legacyId = new AssetId(Guid.NewGuid());
            await using var db = new SqliteAssetDatabase(Path.Combine(root, "assets.db"));
            await db.InitializeAsync(CancellationToken.None);
            await db.UpsertAssetAsync(
                new AssetDbAssetRecord(legacyId, "cube.hlsl", new AssetTypeId("shader"), "legacy", 0),
                CancellationToken.None);

            var catalog = new AssetCatalog("sandbox", index, db);
            var record = catalog.GetOrAdd(node!.Id, new AssetTypeId("shader"));

            var snapshot = await db.CaptureSnapshotAsync(CancellationToken.None);
            var asset = Assert.Single(snapshot.Assets);
            Assert.Equal(record.AssetId, asset.AssetId);
            Assert.NotEqual(legacyId, asset.AssetId);
            Assert.Equal("cube.hlsl", asset.LogicalPath);
        }
        finally
        {
            TestTempDirectory.Delete(root);
        }
    }
}
