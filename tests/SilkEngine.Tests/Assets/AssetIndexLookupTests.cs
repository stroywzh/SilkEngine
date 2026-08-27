using SilkEngine.Assets;
using SilkEngine.Assets.Importer;
using SilkEngine.Assets.VirtualFileSystem;
using SilkEngine.Core;
using SilkEngine.Tests.Core;

namespace SilkEngine.Tests.Assets;

/// <summary>
/// VFS 严格索引测试：路径加载必须先经启动扫描进入索引；未索引路径抛详细
/// InvalidOperationException 且无目录/导入副作用；目录路径拒绝加载。
/// </summary>
[Collection("Assets")]
public class AssetIndexLookupTests : IDisposable
{
    /// <summary>测试级清理：注销 ctor 自注册的 AssetManager 实例（Unregister 幂等）</summary>
    public void Dispose() => Services.Unregister<AssetManager>();

    [Fact]
    public void Load_UnindexedPath_ThrowsDetailedInvalidOperationExceptionWithoutSideEffects()
    {
        using var assets = CreateManagerWithEmptyIndex();

        var ex = Assert.Throws<InvalidOperationException>(
            () => assets.Load<TextureAsset>("Textures/missing.png"));

        Assert.Contains("Textures/missing.png", ex.Message);
        Assert.Contains("VFS index", ex.Message);
        Assert.Contains("startup asset scan", ex.Message);
        Assert.Equal(0, assets.CatalogCountForTests);
    }

    [Fact]
    public void Load_IndexedDirectory_ThrowsInvalidOperationException()
    {
        using var assets = CreateManagerWithIndex(ScanFile.Directory("Textures"));

        var ex = Assert.Throws<InvalidOperationException>(
            () => assets.Load<TextureAsset>("Textures"));

        Assert.Contains("directory", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static AssetManager CreateManagerWithEmptyIndex() => CreateManagerWithIndex();

    private static AssetManager CreateManagerWithIndex(params ScanFile[] files)
    {
        var index = new InMemoryVirtualFileIndex();
        if (files.Length > 0)
            index.Apply(ScanResult.FromFiles(files));
        return new AssetManager(
            new InMemoryAssetFileSystem("Assets"),
            index,
            new AssetImporterRegistry(),
            new RecordingScheduler());
    }
}
