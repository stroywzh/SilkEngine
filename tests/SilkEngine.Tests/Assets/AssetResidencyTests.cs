using SilkEngine.Assets;
using SilkEngine.Assets.VirtualFileSystem;
using SilkEngine.Core;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Threading;
using SilkEngine.Tests.Core;
using SilkEngine.Tests.Core.Assets;

namespace SilkEngine.Tests.Assets;

/// <summary>
/// 驻留与卸载测试：Slot/Lease/Pin 持有期间 UnloadUnused 不驱逐；
/// 无持有者的 Payload 被驱逐并经 GPU 句柄映射生成无资产语义的释放请求。
/// </summary>
[Collection("Assets")]
public class AssetResidencyTests : IDisposable
{
    /// <summary>测试级清理：注销 ctor 自注册的 AssetManager 实例（Unregister 幂等）</summary>
    public void Dispose() => Services.Unregister<AssetManager>();

    [Fact]
    public void UnloadUnused_DoesNotEvictPayloadHeldBySlotOrPin()
    {
        var assets = CreateManagerWithReadyTexture(out var handle, out var texture);
        using var slot = assets.CreateSlot(handle);
        using var pin = assets.Pin(handle);

        assets.UnloadUnused();

        Assert.True(assets.TryResolve(handle, out TextureAsset? resolved));
        Assert.Same(texture, resolved);
    }

    [Fact]
    public void UnloadUnused_EvictsUnheldPayloadAndQueuesRenderRelease()
    {
        var assets = CreateManagerWithReadyTexture(out var handle, out _);

        assets.UnloadUnused();

        Assert.False(assets.TryResolve(handle, out TextureAsset? _));
        Assert.True(assets.TryDequeueRenderRelease(out var request));
        Assert.Equal(RenderResourceKind.Texture, request.Kind);
        Assert.NotEqual(0UL, request.Handle);
    }

    [Fact]
    public void SlotDispose_ReleasesResidency_AndPayloadBecomesEvictable()
    {
        var assets = CreateManagerWithReadyTexture(out var handle, out _);
        var slot = assets.CreateSlot(handle);

        slot.Dispose();
        assets.UnloadUnused();

        Assert.False(assets.TryResolve(handle, out TextureAsset? _));
    }

    /// <summary>测试辅助：已索引纹理 + Ready 缓存 + 已发布 GPU 句柄的管理器</summary>
    private static AssetManager CreateManagerWithReadyTexture(
        out AssetHandle<TextureAsset> handle,
        out TextureAsset texture)
    {
        var files = new InMemoryAssetFileSystem("Assets");
        files.Add("T.png", PngFixtures.RedPng);
        var context = TestAssetPipeline.CreateContext(files, index =>
            index.Apply(ScanResult.FromFiles([ScanFile.File("T.png", 1)])));
        texture = context.Manager.Load<TextureAsset>("T.png");
        context.Runtime.Drain(MainThreadPhase.FrameCommit);
        var entry = Assert.Single(context.Manager.Cache.All());
        handle = new AssetHandle<TextureAsset>(entry.AssetId);
        context.Manager.PublishRenderTexture(entry.AssetId, new RenderTextureHandle(9));
        return context.Manager;
    }
}
