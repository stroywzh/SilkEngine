using SilkEngine.Core;
using SilkEngine.Assets;
using SilkEngine.Render.OpenGL;
using SilkEngine.Tests.Core.Assets;

namespace SilkEngine.Tests.Render;

// 与 Part 2 资产测试同集合：本类读写实例缓存（每测试新建 AssetManager），串行执行保险
[Collection("Assets")]
public class TextureUnloadTests : IDisposable
{
    /// <summary>测试级清理：注销测试内 ctor 自注册的 AssetManager 实例（Unregister 幂等）</summary>
    public void Dispose() => Services.Unregister<AssetManager>();

    [Fact]
    public void ReleaseTexture_RemovesFromCache_AndDisposes()
    {
        var am = new AssetManager(new RecordingScheduler());
        var backend = new OpenGLRenderBackend();
        var tex = new Texture2D
        {
            Name = "T",
            ImageData = new ImageData(1, 1, [255, 255, 255, 255]),
        };
        var glTex = backend.TextureRegistry.GetOrCreate(tex);

        backend.ReleaseTexture(tex);

        Assert.True(glTex.IsDisposed);
        Assert.Equal(0, backend.TextureRegistry.Count);
    }

    [Fact]
    public void ReleaseTexture_UnknownTexture_IsNoOp()
    {
        var am = new AssetManager(new RecordingScheduler());
        var backend = new OpenGLRenderBackend();

        backend.ReleaseTexture(
            new Texture2D { Name = "T", ImageData = new ImageData(1, 1, [1, 1, 1, 1]) }
        );

        Assert.Equal(0, backend.TextureRegistry.Count);
    }

    [Fact]
    public void ProcessUnloadQueue_ForwardsUnloadedTexturesToReleaser()
    {
        var am = new AssetManager(new RecordingScheduler());
        using var file = PngTestFile.Create();
        var tex = am.Load<Texture2D>(file.FilePath);
        am.TryAddRef(tex);
        am.TryRelease(tex);
        am.ProcessCompleted();
        var released = new List<Texture2D>();

        am.ProcessUnloadQueue(a => released.Add((Texture2D)a));

        Assert.Single(released);
        Assert.Same(tex, released[0]);
    }

    [Fact]
    public void UnloadChain_UnloadedTexture_IsReleasedThroughBackend()
    {
        var am = new AssetManager(new RecordingScheduler());
        var backend = new OpenGLRenderBackend();
        using var file = PngTestFile.Create();
        var tex = am.Load<Texture2D>(file.FilePath);
        var glTex = backend.TextureRegistry.GetOrCreate(tex);
        am.TryAddRef(tex);
        am.TryRelease(tex);

        am.ProcessCompleted();
        am.ProcessUnloadQueue(a => backend.ReleaseGpuResource(a));

        Assert.True(glTex.IsDisposed);
        Assert.Equal(0, backend.TextureRegistry.Count);
    }
}
