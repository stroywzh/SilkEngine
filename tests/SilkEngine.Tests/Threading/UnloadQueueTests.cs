using SilkEngine.Core;
using SilkEngine.Assets;
using SilkEngine.Assets.Importer;
using SilkEngine.Assets.VirtualFileSystem;
using SilkEngine.Render;
using SilkEngine.Threading;
using SilkEngine.Tests.Core;

namespace SilkEngine.Tests.Threading;

[Collection("Assets")]
public class UnloadQueueTests
{
    private class FakeBackend : IRenderBackend
    {
        public bool ShouldClose => false;
        public int Width => 800;
        public int Height => 600;
        public Silk.NET.Windowing.IWindow? NativeWindow => null;
        public void InitWindow() { }
        public void MakeContextCurrent() { }
        public void ClearContext() { }
        public void PumpWindowEvents() { }
        public void ExecutePass(IReadOnlyList<DrawCommand> commands) { }
        public void Present() { }
        public IRenderBuffer CreateBuffer(int sizeBytes) => new StubBuffer();
        public void DrawIndirect(IRenderBuffer buffer, int offset, int drawCount) { }
        public void ReleaseTexture(Texture2D texture) { }
        public void ReleaseGpuResource(IAsset asset) { }
        public void Dispose() { }

        private sealed class StubBuffer : IRenderBuffer
        {
            public int SizeBytes => 0;
            public bool IsDisposed => false;
            public void Dispose() { }
        }
    }

    [Fact]
    public void RenderThread_FrameStart_ProcessesUnloadQueue()
    {
        var am = new AssetManager(
            new InMemoryAssetFileSystem("Assets"), new AssetImporterRegistry(), new RecordingScheduler());
        // 渲染线程帧首经 Services.TryGet 解析管理器，ctor 已自注册
        try
        {
            // 准备一个 Unloaded 条目并已入释放队列
            var tex = new Texture2D { Name = "T" };
            var entry = am.Cache.GetOrAdd(new AssetId(Guid.NewGuid()));
            entry.Data = tex;
            entry.State = AssetState.Ready;
            am.TryAddRef(tex);
            am.TryRelease(tex);
            am.ProcessCompleted();
            Assert.Equal(AssetState.Unloaded, entry.State);

            using var fake = new FakeBackend();
            using var exec = new DedicatedThreadExecutor("TestUnload");
            using var loop = new RenderThreadLoop(fake, exec);
            loop.Initialize();
            // 提交一帧 → 渲染线程帧首应处理释放队列
            loop.SubmitFrame([new RenderPass { Commands = [] }]);

            Assert.Null(entry.Data);
        }
        finally
        {
            Services.Unregister<AssetManager>();
        }
    }
}
