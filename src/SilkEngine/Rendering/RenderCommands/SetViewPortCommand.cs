using System.Runtime.InteropServices;
using SilkEngine.Rendering.Attributes;

namespace SilkEngine.Rendering;

/// <summary>Sets the viewport rectangle of the device.</summary>
/// <remarks>Pixel coordinates with a bottom-left origin, as exposed by <c>IRenderDevice.SetViewport</c>.</remarks>
[RenderCommand((int)RenderCommandKind.SetViewport, typeof(SetViewportExecutor))]
public readonly struct SetViewportCommand
{
    /// <summary>Left edge in pixels.</summary>
    public readonly int X;

    /// <summary>Bottom edge in pixels.</summary>
    public readonly int Y;

    /// <summary>Viewport width in pixels.</summary>
    public readonly int Width;

    /// <summary>Viewport height in pixels.</summary>
    public readonly int Height;

    /// <summary>Creates the command.</summary>
    /// <param name="x">Left edge in pixels.</param>
    /// <param name="y">Bottom edge in pixels.</param>
    /// <param name="width">Viewport width in pixels.</param>
    /// <param name="height">Viewport height in pixels.</param>
    public SetViewportCommand(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
}

internal static class SetViewportExecutor
{
    public static void Execute(ReadOnlySpan<byte> param, ref CommandContext ctx)
    {
        var cmd = MemoryMarshal.Read<SetViewportCommand>(param);
        ctx.Device.SetViewport(cmd.X, cmd.Y, cmd.Width, cmd.Height);
    }
}
