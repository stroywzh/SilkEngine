using System.Runtime.InteropServices;
using SilkEngine.Rendering.Attributes;

namespace SilkEngine.Rendering;

/// <summary>Draws a range of vertices from the geometry the RHI currently has bound.</summary>
[RenderCommand((int)RenderCommandKind.Draw, typeof(DrawCommandExecutor))]
public readonly struct DrawCommand
{
    public readonly int VertexCount;
    public readonly int FirstVertex;

    public DrawCommand(int vertexCount, int firstVertex)
    {
        VertexCount = vertexCount;
        FirstVertex = firstVertex;
    }
}

internal static class DrawCommandExecutor
{
    public static void Execute(ReadOnlySpan<byte> param, ref CommandContext ctx)
    {
        var cmd = MemoryMarshal.Read<DrawCommand>(param);
        ctx.Device.Draw(cmd.VertexCount, cmd.FirstVertex);
    }
}
