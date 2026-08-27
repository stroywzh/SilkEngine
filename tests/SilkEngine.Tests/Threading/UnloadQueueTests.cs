using SilkEngine.Core;
using SilkEngine.Assets;
using SilkEngine.Render;
using SilkEngine.Threading;
using SilkEngine.Tests.Core.Assets;

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
        public void ReleaseTexture(TextureAsset texture) { }
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
        var am = TestAssetPipeline.CreateManager();
        // 渲染线程帧首经 Services.TryGet 解析管理器，ctor 已自注册
        try
        {
            using var fake = new FakeBackend();
            using var exec = new DedicatedThreadExecutor("TestUnload");
            using var loop = new RenderThreadLoop(fake, exec);
            loop.Initialize();
            // 提交一帧 → 渲染线程帧首应排空释放请求队列（新模型：驱逐在 UnloadUnused 接入，当前无请求）
            loop.SubmitFrame([new RenderPass { Commands = [] }]);

            Assert.False(am.TryDequeueRenderRelease(out _));
        }
        finally
        {
            Services.Unregister<AssetManager>();
        }
    }
}
