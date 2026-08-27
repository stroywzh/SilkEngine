using SilkEngine.Core;
using SilkEngine.Assets;
using SilkEngine.Rendering;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.Backend;
using SilkEngine.Threading;
using SilkEngine.Tests.Core.Assets;

namespace SilkEngine.Tests.Threading;

/// <summary>
/// 渲染线程帧首排空 release-request 队列契约：AssetManager 驱逐入队 → 渲染线程帧首
/// 经主线程注入的排空器逐条交给 backend.Release（Rendering 域零 Assets 引用）。
/// </summary>
[Collection("Assets")]
public class UnloadQueueTests
{
    private sealed class RecordingBackend : IRenderBackend
    {
        public List<RenderResourceReleaseRequest> Releases = [];

        public void Initialize() { }

        public void Execute(RenderPacket packet) { }

        public void Present() { }

        public void Release(RenderResourceReleaseRequest request) => Releases.Add(request);

        public void Dispose() { }
    }

    [Fact]
    public void RenderThread_FrameStart_DrainsReleaseQueueIntoBackendRelease()
    {
        var am = TestAssetPipeline.CreateManager();
        try
        {
            // 已发布 GPU 句柄的资产驱逐 → release-request 入队（尚未有渲染线程消费）
            var entry = am.Cache.GetOrAdd(new AssetId(Guid.NewGuid()));
            entry.Payload = new TextureAsset("T", new ImageData(1, 1, [255, 255, 255, 255]));
            entry.State = AssetState.Ready;
            am.PublishRenderTexture(entry.AssetId, new RenderTextureHandle(7));
            am.UnloadUnused();
            Assert.False(am.TryResolve(new AssetHandle<TextureAsset>(entry.AssetId), out TextureAsset? _)); // 已驱逐

            using var runtime = new ThreadRuntime();
            runtime.RegisterMainThread();
            var backend = new RecordingBackend();
            using var host = new RenderThreadHost(runtime, backend);
            runtime.RegisterManagedLoop(host);
            host.DrainUnloadQueue = am.ProcessUnloadQueue; // 主线程接线：Assets 队列 → 渲染线程 backend.Release
            host.Start();

            host.SubmitFrame([]); // 帧首排空

            Assert.Equal(7UL, Assert.Single(backend.Releases).Handle);
            Assert.False(am.TryDequeueRenderRelease(out _));
        }
        finally
        {
            Services.Unregister<AssetManager>();
        }
    }
}
