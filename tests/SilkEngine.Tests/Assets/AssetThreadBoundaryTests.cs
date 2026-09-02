using System.Collections.Concurrent;
using SilkEngine.Assets;
using SilkEngine.Assets.Importer;
using SilkEngine.Assets.VirtualFileSystem;
using SilkEngine.Threading;
using SilkEngine.Tests.Core;

namespace SilkEngine.Tests.Assets;

/// <summary>
/// 资产管线线程边界测试：Read/Import 在 Worker 域执行；成功结果经 FrameCommit 阶段在 Main 域投递。
/// </summary>
public class AssetThreadBoundaryTests
{
    private static readonly AssetId TextureAssetId = new(new Guid("11111111-1111-1111-1111-111111111111"));

    private static AssetBuildKey TextureKey() =>
        new(TextureAssetId, AssetImporterRegistry.TextureAssetTypeId, SourceRevision: 0, ImporterRevision: 1, "", "");

    private static AssetPipeline CreatePipeline(IAssetFileSystem files, IAssetImporter importer, ThreadRuntime runtime)
    {
        var index = new InMemoryVirtualFileIndex();
        index.Apply(ScanResult.FromFiles([ScanFile.File("Textures/a.png", 1)]));
        var catalog = new AssetCatalog();
        index.TryGet("Textures/a.png", out var node);
        catalog.Seed(node!.Id, AssetImporterRegistry.TextureAssetTypeId, TextureAssetId);
        var registry = new AssetImporterRegistry(registerDefaults: false);
        registry.Register(AssetImporterRegistry.TextureAssetTypeId, ".png", _ => importer);
        return new AssetPipeline(files, index, catalog, registry, runtime.Background, runtime.MainThread, runtime);
    }

    [Fact]
    public async Task Worker_RunsReadAndImportInWorkerDomain()
    {
        using var runtime = new ThreadRuntime();
        // 专用 Main 线程登记：xUnit 测试线程是线程池线程，await 后可能被 Task.Run 复用（线程身份误判 Main）
        var main = ThreadFactory.CreateThread(runtime.RegisterMainThread, "TestMain");
        main.Start();
        main.Join();
        var importer = new DomainRecordingImporter(runtime);
        var files = new InMemoryAssetFileSystem("Assets");
        files.Add("Textures/a.png", PngFixtures.RedPng);
        var pipeline = CreatePipeline(files, importer, runtime);

        var result = await pipeline.Request<TextureAsset>(TextureKey()).AsTask();

        Assert.NotNull(result);
        Assert.Equal(ThreadDomain.Worker, Assert.Single(importer.ObservedDomains));
    }

    [Fact]
    public async Task SuccessfulResult_IsPostedToFrameCommit_AndRunsOnMain()
    {
        using var runtime = new ThreadRuntime();
        using var start = new ManualResetEventSlim();
        using var done = new ManualResetEventSlim();
        var applied = new TaskCompletionSource<AssetPipelineResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var applyDomain = ThreadDomain.Unknown;
        var main = ThreadFactory.CreateThread(() =>
        {
            runtime.RegisterMainThread();
            start.Wait();
            for (var i = 0; i < 100 && !applied.Task.IsCompleted; i++)
            {
                runtime.Drain(MainThreadPhase.FrameCommit);
                Thread.Sleep(5);
            }
            done.Set();
        }, "TestMain");
        main.Start();

        var importer = new CountingTextureImporter();
        var files = new InMemoryAssetFileSystem("Assets");
        files.Add("Textures/a.png", PngFixtures.RedPng);
        var pipeline = CreatePipeline(files, importer, runtime);
        pipeline.ResultSink = result =>
        {
            applyDomain = runtime.CurrentDomainForTests;
            applied.TrySetResult(result);
        };

        var operation = pipeline.Request<TextureAsset>(TextureKey());
        start.Set();
        done.Wait(TimeSpan.FromSeconds(10));
        main.Join();

        var result = await applied.Task;
        Assert.Equal(AssetPipelineResultState.Succeeded, result.State);
        Assert.Equal(ThreadDomain.Main, applyDomain);
    }
}

/// <summary>记录导入时线程域的导入器（测试夹具）</summary>
internal sealed class DomainRecordingImporter : IAssetImporter
{
    private readonly ThreadRuntime _runtime;
    private readonly ConcurrentQueue<ThreadDomain> _observed = new();

    public DomainRecordingImporter(ThreadRuntime runtime) => _runtime = runtime;

    public IReadOnlyCollection<ThreadDomain> ObservedDomains => _observed;

    public AssetImportResult Import(ReadOnlyMemory<byte> source, AssetImportContext context)
    {
        _observed.Enqueue(_runtime.CurrentDomainForTests);
        return new AssetImportResult(
            new TextureAsset("domain", new ImageData(1, 1, [255, 255, 255, 255])),
            [],
            ImporterRevision: 1);
    }
}
