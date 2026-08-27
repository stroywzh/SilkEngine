using SilkEngine.Assets;
using SilkEngine.Assets.Importer;
using SilkEngine.Assets.VirtualFileSystem;
using SilkEngine.Core;
using SilkEngine.Threading;
using SilkEngine.Tests.Core.Assets;

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
        using var fx = CreateManagerWithIndex();
        var ex = Assert.Throws<InvalidOperationException>(
            () => fx.Manager.Load<TextureAsset>("Textures/missing.png"));

        Assert.Contains("Textures/missing.png", ex.Message);
        Assert.Contains("VFS index", ex.Message);
        Assert.Contains("startup asset scan", ex.Message);
        Assert.Equal(0, fx.Pipeline.CatalogCountForTests);
    }

    [Fact]
    public void Load_IndexedDirectory_ThrowsInvalidOperationException()
    {
        using var fx = CreateManagerWithIndex(ScanFile.Directory("Textures"));
        var ex = Assert.Throws<InvalidOperationException>(
            () => fx.Manager.Load<TextureAsset>("Textures"));

        Assert.Contains("directory", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ManagerFixture CreateManagerWithIndex(params ScanFile[] files)
    {
        var runtime = new ThreadRuntime();
        runtime.RegisterMainThread();
        var index = new InMemoryVirtualFileIndex();
        if (files.Length > 0)
            index.Apply(ScanResult.FromFiles(files));
        var pipeline = new AssetPipeline(
            new InMemoryAssetFileSystem("Assets"),
            index,
            new AssetCatalog(),
            new AssetImporterRegistry(),
            new SyncBackgroundScheduler(),
            runtime.MainThread,
            runtime);
        return new ManagerFixture(pipeline, runtime);
    }

    /// <summary>索引测试夹具（测试夹具）</summary>
    private sealed class ManagerFixture : IDisposable
    {
        public AssetPipeline Pipeline { get; }

        public AssetManager Manager { get; }

        private readonly ThreadRuntime _runtime;

        public ManagerFixture(AssetPipeline pipeline, ThreadRuntime runtime)
        {
            Pipeline = pipeline;
            _runtime = runtime;
            Manager = new AssetManager(pipeline, runtime.MainThread, runtime);
        }

        public void Dispose()
        {
            Services.Unregister<AssetManager>();
            _runtime.Dispose();
        }
    }
}
