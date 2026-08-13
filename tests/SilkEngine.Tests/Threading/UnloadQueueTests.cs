using SilkEngine.Core.Assets;
using SilkEngine.Render;
using SilkEngine.Threading;

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
        public IntPtr CreateBuffer(int size) => IntPtr.Zero;
        public void DrawIndirect(IntPtr buf, int off, int cnt) { }
        public void Dispose() { }
    }

    [Fact]
    public void RenderThread_FrameStart_ProcessesUnloadQueue()
    {
        // 准备一个 Unloaded 条目并已入释放队列
        var tex = new Texture2D { Name = "T" };
        var entry = AssetManager.Cache.GetOrAdd(Guid.NewGuid());
        entry.Data = tex;
        entry.State = AssetState.Ready;
        AssetManager.TryAddRef(tex);
        AssetManager.TryRelease(tex);
        AssetManager.ProcessCompleted();
        Assert.Equal(AssetState.Unloaded, entry.State);

        using var fake = new FakeBackend();
        using var loop = new RenderThreadLoop(fake);
        loop.Initialize();
        // 提交一帧 → 渲染线程帧首应处理释放队列
        loop.SubmitFrame([new RenderPass { Commands = [] }]);

        Assert.Null(entry.Data);
        loop.Dispose();
    }
}
