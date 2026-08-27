using SilkEngine.Core;
using SilkEngine.Assets;
using SilkEngine.Assets.VirtualFileSystem;
using SilkEngine.Render.OpenGL;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Threading;
using SilkEngine.Tests.Core;
using SilkEngine.Tests.Core.Assets;

namespace SilkEngine.Tests.Render;

// 与 Part 2 资产测试同集合：本类读写实例缓存（每测试新建 AssetManager），串行执行保险
[Collection("Assets")]
public class TextureUnloadTests : IDisposable
{
    /// <summary>测试级清理：注销测试内 ctor 自注册的 AssetManager 实例（Unregister 幂等）</summary>
    public void Dispose() => Services.Unregister<AssetManager>();

    /// <summary>测试辅助：内存文件系统预置红色 PNG（已索引），返回可加载的资产管理器上下文</summary>
    private static ManagerContext CreateManagerContext()
    {
        var files = new InMemoryAssetFileSystem("Assets");
        files.Add("T.png", PngFixtures.RedPng);
        return TestAssetPipeline.CreateContext(files, index =>
            index.Apply(ScanResult.FromFiles([ScanFile.File("T.png", 1)])));
    }

    [Fact]
    public void ReleaseTexture_RemovesFromCache_AndDisposes()
    {
        var context = CreateManagerContext();
        var backend = new OpenGLRenderBackend();
        var tex = new TextureAsset("T", new ImageData(1, 1, [255, 255, 255, 255]));
        var glTex = backend.TextureRegistry.GetOrCreate(tex);

        backend.ReleaseTexture(tex);

        Assert.True(glTex.IsDisposed);
        Assert.Equal(0, backend.TextureRegistry.Count);
    }

    [Fact]
    public void ReleaseTexture_UnknownTexture_IsNoOp()
    {
        var context = CreateManagerContext();
        var backend = new OpenGLRenderBackend();

        backend.ReleaseTexture(new TextureAsset("T", new ImageData(1, 1, [1, 1, 1, 1])));

        Assert.Equal(0, backend.TextureRegistry.Count);
    }

    [Fact]
    public void UnloadUnused_EvictsUnheldTexture_QueuesAssetFreeReleaseRequest()
    {
        var context = CreateManagerContext();
        context.Manager.Load<TextureAsset>("T.png");
        context.Runtime.Drain(MainThreadPhase.FrameCommit);
        var entry = Assert.Single(context.Manager.Cache.All());
        context.Manager.PublishRenderTexture(entry.AssetId, new RenderTextureHandle(7));

        context.Manager.UnloadUnused();

        Assert.False(context.Manager.TryResolve(new AssetHandle<TextureAsset>(entry.AssetId), out TextureAsset? _));
        Assert.True(context.Manager.TryDequeueRenderRelease(out var request));
        Assert.Equal(RenderResourceKind.Texture, request.Kind);
        Assert.Equal(7UL, request.Handle);
    }
}
