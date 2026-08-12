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

        public void ExecuteFrame(IReadOnlyList<DrawCommand> commands)
        {
            Frames.Add(commands);
            if (commands.Count > 0 && commands[0] is SingleDrawCommand sdc && sdc.Mesh?.Name == "Crash")
                throw new InvalidOperationException("simulated crash");
        }

        public void ExecutePass(IReadOnlyList<DrawCommand> commands)
        {
            Frames.Add(commands);
            if (commands.Count > 0 && commands[0] is SingleDrawCommand sdc && sdc.Mesh?.Name == "Crash")
                throw new InvalidOperationException("simulated crash");
        }

        public void Present() { }

        public IntPtr CreateBuffer(int size) => IntPtr.Zero;
        public void DrawIndirect(IntPtr buf, int off, int cnt) { }
        public void Dispose() { }
    }

    [Fact]
    public void SubmitFrame_DeliversCommands()
    {
        using var fake = new FakeBackend();
        var rtl = new RenderThreadLoop(fake);
        rtl.Initialize();
        var cmd = new SingleDrawCommand { Enabled = true };
        rtl.SubmitFrame([cmd]);
        Assert.Single(fake.Frames);
        Assert.Same(cmd, fake.Frames[0][0]);
    }

    [Fact]
    public void ExceptionInExecuteFrame_DoesNotHangSubmitFrame()
    {
        using var fake = new FakeBackend();
        var rtl = new RenderThreadLoop(fake);
        rtl.Initialize();
        var badCmd = new SingleDrawCommand { Mesh = new Mesh { Name = "Crash", Vertices = [], Layout = [] } };
        rtl.SubmitFrame([badCmd]); // 不应挂起
        var goodCmd = new SingleDrawCommand { Enabled = true };
        rtl.SubmitFrame([goodCmd]);
        Assert.True(fake.Frames.Count >= 1);
    }
}
