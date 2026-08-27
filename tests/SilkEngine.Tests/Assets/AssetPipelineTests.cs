using SilkEngine.Assets;
using SilkEngine.Assets.Importer;
using SilkEngine.Assets.VirtualFileSystem;
using SilkEngine.Threading;
using SilkEngine.Tests.Core;

namespace SilkEngine.Tests.Assets;

/// <summary>
/// Pipeline 协调测试：BuildKey 去重（单读单导入）、依赖循环失败携带依赖链、
/// 源修订变更后过期结果不发布（AssetStaleResultException）。
/// </summary>
public class AssetPipelineTests
{
    private static readonly AssetId TextureAssetId = new(new Guid("11111111-1111-1111-1111-111111111111"));
    internal static readonly AssetId AssetA = new(new Guid("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA"));
    internal static readonly AssetId AssetB = new(new Guid("BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB"));
    internal static readonly AssetTypeId TestType = new("test");

    private static AssetBuildKey TestIndexedTextureKey() =>
        new(TextureAssetId, AssetImporterRegistry.TextureAssetTypeId, SourceRevision: 0, ImporterRevision: 1, "");

    private static AssetBuildKey TestKey(string name) =>
        new(name == "A" ? AssetA : AssetB, TestType, SourceRevision: 0, ImporterRevision: 1, "");

    private static byte[] TestPngBytes() => PngFixtures.RedPng;

    private static AssetPipeline CreatePipeline(IAssetFileSystem files, IAssetImporter importer, ThreadRuntime? runtime = null)
    {
        runtime ??= CreateRuntime();
        var index = new InMemoryVirtualFileIndex();
        index.Apply(ScanResult.FromFiles([ScanFile.File("Textures/a.png", 1)]));
        var catalog = new AssetCatalog();
        index.TryGet("Textures/a.png", out var node);
        catalog.Seed(node!.Id, AssetImporterRegistry.TextureAssetTypeId, TextureAssetId);
        var registry = new AssetImporterRegistry(registerDefaults: false);
        registry.Register(AssetImporterRegistry.TextureAssetTypeId, ".png", _ => importer);
        return new AssetPipeline(files, index, catalog, registry, runtime.Background, runtime.MainThread, runtime);
    }

    private static AssetPipeline CreatePipelineWithDependencies(params (string Name, string Dependency)[] deps)
    {
        var files = new InMemoryAssetFileSystem("Assets");
        files.Add("A.png", [1]);
        files.Add("B.png", [2]);
        var index = new InMemoryVirtualFileIndex();
        index.Apply(ScanResult.FromFiles([ScanFile.File("A.png", 1), ScanFile.File("B.png", 1)]));
        var catalog = new AssetCatalog();
        index.TryGet("A.png", out var nodeA);
        index.TryGet("B.png", out var nodeB);
        catalog.Seed(nodeA!.Id, TestType, AssetA);
        catalog.Seed(nodeB!.Id, TestType, AssetB);
        var importer = new TestPayloadImporter(deps.ToDictionary(d => d.Name, d => d.Dependency));
        var registry = new AssetImporterRegistry(registerDefaults: false);
        registry.Register(TestType, ".png", _ => importer);
        var runtime = CreateRuntime();
        return new AssetPipeline(files, index, catalog, registry, runtime.Background, runtime.MainThread, runtime);
    }

    private static AssetPipeline CreateControllablePipeline(out Action releaseImport)
    {
        var files = new CountingAssetFileSystem("Textures/a.png", TestPngBytes());
        var importer = new GatedTextureImporter();
        releaseImport = importer.Release;
        return CreatePipeline(files, importer);
    }

    private static ThreadRuntime CreateRuntime()
    {
        var runtime = new ThreadRuntime();
        runtime.RegisterMainThread();
        return runtime;
    }

    [Fact]
    public async Task Request_SameBuildKey_DeduplicatesReadAndImport()
    {
        var files = new CountingAssetFileSystem("Textures/a.png", TestPngBytes());
        var importer = new CountingTextureImporter();
        using var runtime = CreateRuntime();
        var pipeline = CreatePipeline(files, importer, runtime);
        var key = TestIndexedTextureKey();

        var first = pipeline.Request<TextureAsset>(key);
        var second = pipeline.Request<TextureAsset>(key);
        var results = await Task.WhenAll(first.AsTask(), second.AsTask());

        Assert.Same(results[0], results[1]);
        Assert.Equal(1, files.ReadCount);
        Assert.Equal(1, importer.ImportCount);
    }

    [Fact]
    public async Task Request_CyclicDependencies_FailsWithDependencyChain()
    {
        using var runtime = CreateRuntime();
        var pipeline = CreatePipelineWithDependencies(("A", "B"), ("B", "A"));

        var ex = await Assert.ThrowsAsync<InvalidDataException>(
            async () => await pipeline.Request<TestPayload>(TestKey("A")).AsTask());

        Assert.Contains("A", ex.Message);
        Assert.Contains("B", ex.Message);
    }

    [Fact]
    public async Task StaleResult_IsNotPublishedAfterSourceRevisionChanges()
    {
        using var runtime = CreateRuntime();
        var pipeline = CreateControllablePipeline(out var releaseImport);
        var operation = pipeline.Request<TextureAsset>(TestIndexedTextureKey());
        pipeline.Invalidate(TestIndexedTextureKey().AssetId);
        releaseImport();

        await Assert.ThrowsAsync<AssetStaleResultException>(async () => await operation.AsTask());
    }
}

/// <summary>按读取次数计数的文件系统夹具（测试夹具）</summary>
internal sealed class CountingAssetFileSystem : IAssetFileSystem
{
    private readonly InMemoryAssetFileSystem _inner = new("Assets");

    public CountingAssetFileSystem(string path, byte[] content) => _inner.Add(path, content);

    public int ReadCount { get; private set; }

    public string Normalize(string path) => _inner.Normalize(path);

    public bool Exists(string path) => _inner.Exists(path);

    public ValueTask<ReadOnlyMemory<byte>> ReadAsync(string path)
    {
        ReadCount++;
        return _inner.ReadAsync(path);
    }

    public ValueTask<FileMetadata> GetMetadataAsync(string path) => _inner.GetMetadataAsync(path);
}

/// <summary>按导入次数计数的纹理导入器夹具（测试夹具）</summary>
internal sealed class CountingTextureImporter : IAssetImporter
{
    public int ImportCount { get; private set; }

    public AssetImportResult Import(ReadOnlyMemory<byte> source, AssetImportContext context)
    {
        ImportCount++;
        var name = context.Path is { Length: > 0 } path ? Path.GetFileNameWithoutExtension(path) : "Texture";
        return new AssetImportResult(
            new TextureAsset(name, new ImageData(1, 1, [255, 255, 255, 255])),
            [],
            ImporterRevision: 1);
    }
}

/// <summary>依赖表驱动的测试载荷导入器：按源路径返回声明依赖（测试夹具）</summary>
internal sealed class TestPayloadImporter : IAssetImporter
{
    private readonly Dictionary<string, string> _dependencies;

    public TestPayloadImporter(Dictionary<string, string> dependencies) => _dependencies = dependencies;

    public AssetImportResult Import(ReadOnlyMemory<byte> source, AssetImportContext context)
    {
        var name = Path.GetFileNameWithoutExtension(context.Path);
        var dependency = _dependencies.TryGetValue(name, out var dep) ? dep : null;
        var handles = dependency is null
            ? []
            : new[]
            {
                new UntypedAssetHandle(
                    dependency == "B" ? AssetPipelineTests.AssetB : AssetPipelineTests.AssetA,
                    AssetPipelineTests.TestType),
            };
        return new AssetImportResult(new TestPayload(name), handles, ImporterRevision: 1);
    }
}

/// <summary>测试载荷（测试夹具）</summary>
internal sealed class TestPayload(string name) : IAssetPayload
{
    public string Name { get; } = name;
}

/// <summary>门控导入器：Release 前阻塞导入（测试夹具）</summary>
internal sealed class GatedTextureImporter : IAssetImporter
{
    private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public AssetImportResult Import(ReadOnlyMemory<byte> source, AssetImportContext context)
    {
        _gate.Task.GetAwaiter().GetResult();
        return new AssetImportResult(
            new TextureAsset("gated", new ImageData(1, 1, [255, 255, 255, 255])),
            [],
            ImporterRevision: 1);
    }

    public void Release() => _gate.TrySetResult();
}
