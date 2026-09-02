using SilkEngine.Assets;
using SilkEngine.Core;

namespace SilkEngine.Tests.Assets;

/// <summary>
/// 取消隔离契约（任务 9）：共享 Pipeline Job 与 consumer operation 分离——
/// 单个调用方取消只完成自己的操作；共享构建不因一方取消而中断。
/// </summary>
[Collection("Assets")]
public class AssetCancellationTests : IDisposable
{
    /// <summary>测试级清理：注销 ctor 自注册的 AssetManager 实例（Unregister 幂等）</summary>
    public void Dispose() => Services.Unregister<AssetManager>();

    [Fact]
    public async Task CancellingOneConsumer_DoesNotCancelSharedBuild()
    {
        using var fixture = TestAssetPipelineFixture.Blocking("Textures/ShoreKeeper1.png");
        using var firstCts = new CancellationTokenSource();
        var first = fixture.Manager.LoadAsync<TextureAsset>("Textures/ShoreKeeper1.png", firstCts.Token);
        var second = fixture.Manager.LoadAsync<TextureAsset>("Textures/ShoreKeeper1.png");

        firstCts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first.AsTask());
        fixture.ReleaseRead();

        Assert.NotNull(await second.AsTask());
    }
}