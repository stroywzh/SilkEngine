using System.Numerics;
using Xunit;

namespace SilkEngine.Rendering.Tests;

public sealed class RenderCommandTests
{
    [Fact]
    public void ClearColor_RecordsKindTargetAndColor()
    {
        var target = new RenderTargetHandle(17);
        var color = new Vector4(0.25f, 0.5f, 0.75f, 1f);

        var command = RenderCommand.ClearColor(target, color);

        Assert.Equal(RenderCommandKind.ClearColor, command.Kind);
        Assert.Equal((ushort)17, command.Target.Id);
        Assert.Equal(color, command.Color);
    }

    [Fact]
    public void ClearColor_DoesNotTreatZeroAsBackbuffer()
    {
        var command = RenderCommand.ClearColor(new RenderTargetHandle(0), Vector4.Zero);

        Assert.Equal((ushort)0, command.Target.Id);
        Assert.Equal(RenderCommandKind.ClearColor, command.Kind);
    }
}
