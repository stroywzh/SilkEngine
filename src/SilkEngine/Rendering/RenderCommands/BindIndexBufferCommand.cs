using System.Runtime.InteropServices;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.Attributes;

namespace SilkEngine.Rendering;

/// <summary>Binds an index buffer and remembers the element format used by indexed draws.</summary>
[RenderCommand((int)RenderCommandKind.BindIndexBuffer, typeof(BindIndexBufferExecutor))]
public readonly struct BindIndexBufferCommand
{
    /// <summary>Index buffer to bind.</summary>
    public readonly BufferHandle Buffer;

    /// <summary>Element format of the index buffer.</summary>
    public readonly IndexFormat Format;

    /// <summary>Creates the command.</summary>
    /// <param name="buffer">Index buffer to bind.</param>
    /// <param name="format">Element format of the index buffer.</param>
    public BindIndexBufferCommand(BufferHandle buffer, IndexFormat format)
    {
        Buffer = buffer;
        Format = format;
    }
}

internal static class BindIndexBufferExecutor
{
    public static void Execute(ReadOnlySpan<byte> param, ref CommandContext ctx)
    {
        var cmd = MemoryMarshal.Read<BindIndexBufferCommand>(param);
        ctx.Device.BindIndexBuffer(cmd.Buffer, cmd.Format);
    }
}
