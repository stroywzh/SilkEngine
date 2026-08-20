using SilkEngine.Core.Assets;
using SilkEngine.Render;
using SilkEngine.Threading;

namespace SilkEngine.Tests.Threading;

public class RenderThreadLoopTests
{
    private class FakeBackend : IRenderBackend
    {
        public List<IReadOnlyList<DrawCommand>> Frames = new();
        public bool ShouldCloseVal;
        public bool ShouldClose => ShouldCloseVal;
        public int Width => 800;
        public int Height => 600;
        public Silk.NET.Windowing.IWindow? NativeWindow => null;
        public void InitWindow() { }
        public void MakeContextCurrent() { }
        public void ClearContext() { }
        public void PumpWindowEvents() { }

        public void ExecutePass(IReadOnlyList<DrawCommand> commands)
        {
            Frames.Add(commands);
            if (commands.Count > 0 && commands[0] is SingleDrawCommand sdc && sdc.Mesh?.Name == "Crash")
                throw new InvalidOperationException("simulated crash");
        }

        public void Present() { }

        public IRenderBuffer CreateBuffer(int sizeBytes) => new StubBuffer();
        public void DrawIndirect(IRenderBuffer buffer, int offset, int drawCount) { }
        public void ReleaseTexture(Texture2D texture) { }
        public void Dispose() { }

        private sealed class StubBuffer : IRenderBuffer
        {
            public int SizeBytes => 0;
            public bool IsDisposed => false;
            public void Dispose() { }
        }
    }

    [Fact]
    public void SubmitFrame_DeliversCommands()
    {
        using var fake = new FakeBackend();
        using var exec = new DedicatedThreadExecutor("TestRender");
        using var rtl = new RenderThreadLoop(fake, exec);
        rtl.Initialize();
        var cmd = new SingleDrawCommand { Enabled = true };
        rtl.SubmitFrame([new RenderPass { Commands = [cmd] }]);
        Assert.Single(fake.Frames);
        Assert.Same(cmd, fake.Frames[0][0]);
    }

    [Fact]
    public void SubmitFrame_WithPasses_ExecutesAllPasses()
    {
        using var fake = new FakeBackend();
        using var exec = new DedicatedThreadExecutor("TestRender");
        using var rtl = new RenderThreadLoop(fake, exec);
        rtl.Initialize();

        var cmd1 = new SingleDrawCommand { Enabled = true, Mesh = new Mesh { Name = "A" } };
        var cmd2 = new SingleDrawCommand { Enabled = true, Mesh = new Mesh { Name = "B" } };
        var passes = new List<RenderPass>
        {
            new() { SortOrder = 0, Commands = [cmd1] },
            new() { SortOrder = 1, Commands = [cmd2] }
        };

        rtl.SubmitFrame(passes);
        Assert.Equal(2, fake.Frames.Count);
        Assert.Same(cmd1, fake.Frames[0][0]);
        Assert.Same(cmd2, fake.Frames[1][0]);
    }

    [Fact]
    public void ExceptionInExecutePass_DoesNotHangSubmitFrame()
    {
        using var fake = new FakeBackend();
        using var exec = new DedicatedThreadExecutor("TestRender");
        using var rtl = new RenderThreadLoop(fake, exec);
        rtl.Initialize();
        var badCmd = new SingleDrawCommand { Mesh = new Mesh { Name = "Crash", Vertices = [], Layout = [] } };
        rtl.SubmitFrame([new RenderPass { Commands = [badCmd] }]); // 不应挂起
        var goodCmd = new SingleDrawCommand { Enabled = true };
        rtl.SubmitFrame([new RenderPass { Commands = [goodCmd] }]);
        Assert.True(fake.Frames.Count >= 1);
    }
}
