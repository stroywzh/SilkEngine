using SilkEngine.Core.Assets;
using SilkEngine.Render;
using SilkEngine.Threading;

namespace SilkEngine.Tests.Threading;

public class RenderThreadLoopHandshakeTests
{
    private sealed class FakeBackend : IRenderBackend
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
        public void ReleaseTexture(Texture2D texture) { }
        public void Dispose() { }
    }

    private sealed class SilentExecutor : ILoopExecutor
    {
        public bool Stopped;
        public bool Joined;
        public string Name => "Silent";
        public ThreadContext? Context => null;
        public IJobHandle Run(Func<bool> frame) => new TaskJobHandle(Task.CompletedTask);
        public void Stop() => Stopped = true;
        public void Join() => Joined = true;
        public void Dispose() { }
    }

    [Fact]
    public void SubmitFrame_RenderThreadSilent_ThrowsTimeout()
    {
        var executor = new SilentExecutor();
        using var loop = new RenderThreadLoop(new FakeBackend(), executor);
        loop.FrameTimeout = TimeSpan.FromMilliseconds(200); // 可注入时间源（测试缩短）
        var ex = Assert.Throws<TimeoutException>(() => loop.SubmitFrame([]));
        Assert.Contains("timeout", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dispose_DoesNotStopOrJoinExecutor()
    {
        var executor = new SilentExecutor();
        var loop = new RenderThreadLoop(new FakeBackend(), executor);
        loop.Dispose();
        Assert.False(executor.Stopped);
        Assert.False(executor.Joined);
    }
}
