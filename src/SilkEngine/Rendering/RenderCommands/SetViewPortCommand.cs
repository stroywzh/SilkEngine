using System.Runtime.InteropServices;
using SilkEngine.Rendering.Attributes;

namespace SilkEngine.Rendering;

[RenderCommand((int)RenderCommandKind.SetViewport, typeof(SetViewportExecutor))]
public readonly struct SetViewportCommand
{
    public readonly int X;
    public readonly int Y;
    public readonly int Width;
    public readonly int Height;

    public SetViewportCommand(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
}

public static class SetViewportExecutor
{
    public static void Execute(ReadOnlySpan<byte> param, ref CommandContext ctx)
    {
        var cmd = MemoryMarshal.Read<SetViewportCommand>(param);
        ctx.RHI.SetViewport(cmd.X, cmd.Y, cmd.Width, cmd.Height);
    }
}
