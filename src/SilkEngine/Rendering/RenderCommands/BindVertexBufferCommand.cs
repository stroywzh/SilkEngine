using System.Runtime.InteropServices;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.Attributes;

namespace SilkEngine.Rendering;

/// <summary>Binds a vertex buffer; prototype convention is attribute 0 = position (two floats).</summary>
[RenderCommand((int)RenderCommandKind.BindVertexBuffer, typeof(BindVertexBufferExecutor))]
public readonly struct BindVertexBufferCommand
{
    /// <summary>Vertex buffer to bind.</summary>
    public readonly BufferHandle Buffer;

    /// <summary>Bytes between consecutive vertices.</summary>
    public readonly uint Stride;

    /// <summary>Creates the command.</summary>
    /// <param name="buffer">Vertex buffer to bind.</param>
    /// <param name="stride">Bytes between consecutive vertices.</param>
    public BindVertexBufferCommand(BufferHandle buffer, uint stride)
    {
        Buffer = buffer;
        Stride = stride;
    }
}

internal static class BindVertexBufferExecutor
{
    public static void Execute(ReadOnlySpan<byte> param, ref CommandContext ctx)
    {
        var cmd = MemoryMarshal.Read<BindVertexBufferCommand>(param);
        ctx.Device.BindVertexBuffer(cmd.Buffer, cmd.Stride);
    }
}
