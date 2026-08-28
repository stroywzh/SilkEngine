using SilkEngine.Assets;
using SilkEngine.Assets.Importer;
using SilkEngine.Assets.VirtualFileSystem;
using SilkEngine.Core;
using SilkEngine.Threading;
using SilkEngine.Tests.Core;
using SilkEngine.Tests.Core.Assets;

namespace SilkEngine.Tests.Assets;

/// <summary>
/// 安全异步操作测试：默认 await 在 Main 域恢复；AsTask 可组合标准 Task；
/// 单调用方 Cancel 不影响共享操作的其它消费者。
/// </summary>
[Collection("Assets")]
public class AssetOperationTests : IDisposable
{
    /// <summary>测试级清理：注销 ctor 自注册的 AssetManager 实例（Unregister 幂等）</summary>
    public void Dispose() => Services.Unregister<AssetManager>();

    /// <summary>在当前（xUnit）线程登记 Main 域并装配自注册 AssetManager 的线程运行时</summary>
    private static ThreadRuntime TestRuntimeOnCurrentThread()
    {
        var runtime = new ThreadRuntime();
        runtime.RegisterMainThread();
        var manager = new AssetManager(
            new AssetPipeline(
                new InMemoryAssetFileSystem("Assets"),
                new InMemoryVirtualFileIndex(),
                new AssetCatalog(),
                new AssetImporterRegistry(),
                new SyncBackgroundScheduler(),
                runtime.MainThread,
                runtime),
            runtime.MainThread,
            runtime);
        Services.Register(manager); // AssetOperation.FromTask 静态门面经 Services 取用
        return runtime;
    }

    private static ImageData TestImage() => new(1, 1, [255, 255, 255, 255]);

    [Fact]
    public async Task Await_DefaultsToMainSafeContinuation()
    {
        using var runtime = TestRuntimeOnCurrentThread();
        var operation = AssetOperation<TextureAsset>.FromTask(Task.FromResult(new TextureAsset("t", TestImage())));
        var observed = ThreadDomain.Unknown;

        await operation;
        observed = runtime.CurrentDomainForTests;

        Assert.Equal(ThreadDomain.Main, observed);
    }

    [Fact]
    public async Task AsTask_ComposesWithWhenAll_AndFromTaskReentersSafeModel()
    {
        using var runtime = TestRuntimeOnCurrentThread();
        var task = Task.FromResult(new TextureAsset("t", TestImage()));
        var operation = AssetOperation<TextureAsset>.FromTask(task);

        var result = await Task.WhenAll(operation.AsTask());
        var wrapped = AssetOperation<TextureAsset>.FromTask(Task.FromResult(result[0]));

        Assert.Same(result[0], await wrapped);
    }

    [Fact]
    public async Task Cancel_OnlyCancelsCurrentOperation()
    {
        using var runtime = TestRuntimeOnCurrentThread();
        var gate = new TaskCompletionSource<TextureAsset>(TaskCreationOptions.RunContinuationsAsynchronously);
        var manager = Services.Get<AssetManager>();
        var first = manager.WrapExternalTask(gate.Task);
        var second = manager.WrapExternalTask(gate.Task);

        first.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await first);
        Assert.False(second.IsCompleted);
    }
}
