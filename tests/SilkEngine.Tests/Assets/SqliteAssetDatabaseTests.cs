using SilkEngine.Assets;
using SilkEngine.Assets.Database;

namespace SilkEngine.Tests.Assets;

/// <summary>
/// SqliteAssetDatabase 契约测试：重启持久化与损坏备份重建。
/// </summary>
public class SqliteAssetDatabaseTests
{
    [Fact]
    public async Task CreateOpenAndReopen_PreservesAssetAndBuildMetadata()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        var path = Path.Combine(root, "assetdb.sqlite");
        await using (var db = new SqliteAssetDatabase(path))
        {
            await db.InitializeAsync(CancellationToken.None);
            var assetId = new AssetId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
            await db.UpsertAssetAsync(new AssetDbAssetRecord(
                assetId, "Materials/Cube.asset", new AssetTypeId("material"),
                "hash-a", 3), CancellationToken.None);
            await db.UpsertBuildAsync(new AssetDbBuildRecord(
                assetId, "build-a", "cache/build-a.bin", "hash-a"), CancellationToken.None);
        }

        await using var reopened = new SqliteAssetDatabase(path);
        await reopened.InitializeAsync(CancellationToken.None);
        var record = await reopened.GetAssetAsync("Materials/Cube.asset", CancellationToken.None);
        var build = await reopened.GetBuildAsync("build-a", CancellationToken.None);

        Assert.Equal("hash-a", record!.SourceFingerprint);
        Assert.Equal("cache/build-a.bin", build!.CachePath);
    }

    [Fact]
    public async Task CorruptDatabase_IsReportedAndCanBeRebuilt()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        var path = Path.Combine(root, "assetdb.sqlite");
        await File.WriteAllTextAsync(path, "not sqlite");

        await using var db = new SqliteAssetDatabase(path);
        var exception = await Assert.ThrowsAsync<AssetDatabaseCorruptException>(
            () => db.InitializeAsync(CancellationToken.None).AsTask());

        Assert.Equal(path, exception.DatabasePath);
        Assert.True(File.Exists(exception.BackupPath));
    }
}
