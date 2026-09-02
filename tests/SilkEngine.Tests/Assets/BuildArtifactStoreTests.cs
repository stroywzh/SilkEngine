using SilkEngine.Assets.Database;

namespace SilkEngine.Tests.Assets;

/// <summary>
/// 构建产物缓存存储测试：按 BuildKey 原子写入/重载，损坏或缺失视为 cache miss。
/// </summary>
public class BuildArtifactStoreTests
{
    [Fact]
    public async Task BuildArtifactStore_WritesAtomicallyAndReloadsByBuildKey()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        var store = new BuildArtifactStore(root);
        var bytes = new byte[] { 1, 2, 3, 4 };

        await store.SaveAsync("build-a", bytes, CancellationToken.None);
        var loaded = await store.LoadAsync("build-a", CancellationToken.None);

        Assert.Equal(bytes, loaded.ToArray());
        Assert.DoesNotContain(
            Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories),
            path => path.EndsWith(".tmp", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MissingOrCorruptArtifact_ReturnsCacheMissWithoutChangingSourceAsset()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        var store = new BuildArtifactStore(root);

        var result = await store.TryLoadAsync("missing", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task CorruptArtifactFile_IsTreatedAsCacheMiss()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        var store = new BuildArtifactStore(root);
        await store.SaveAsync("build-a", new byte[] { 1, 2, 3 }, CancellationToken.None);

        // 直接覆写缓存文件为垃圾字节（模拟撕裂/损坏写入）
        var artifactPath = Assert.Single(Directory.EnumerateFiles(root, "*.bin"));
        await File.WriteAllBytesAsync(artifactPath, [9, 9, 9, 9, 9]);

        var result = await store.TryLoadAsync("build-a", CancellationToken.None);

        Assert.Null(result);
    }
}