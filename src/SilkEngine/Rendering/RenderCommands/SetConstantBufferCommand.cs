using System.Runtime.InteropServices;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.Attributes;

namespace SilkEngine.Rendering;

/// <summary>Binds a byte range of a uniform buffer to a constant-binding point.</summary>
[RenderCommand((int)RenderCommandKind.SetConstantBuffer, typeof(SetConstantBufferExecutor))]
public readonly struct SetConstantBufferCommand
{
    /// <summary>Uniform buffer that owns the range.</summary>
    public readonly BufferHandle Buffer;

    /// <summary>Byte offset of the range inside the buffer.</summary>
    public readonly uint OffsetBytes;

    /// <summary>Size of the range in bytes.</summary>
    public readonly uint SizeBytes;

    /// <summary>Constant-binding point used by the shader's uniform block.</summary>
    public readonly ushort Binding;

    /// <summary>Creates the command.</summary>
    /// <param name="buffer">Uniform buffer that owns the range.</param>
    /// <param name="offsetBytes">Byte offset of the range inside the buffer.</param>
    /// <param name="sizeBytes">Size of the range in bytes.</param>
    /// <param name="binding">Constant-binding point used by the shader's uniform block.</param>
    public SetConstantBufferCommand(
        BufferHandle buffer,
        uint offsetBytes,
        uint sizeBytes,
        ushort binding
    )
    {
        Buffer = buffer;
        OffsetBytes = offsetBytes;
        SizeBytes = sizeBytes;
        Binding = binding;
    }
}

internal static class SetConstantBufferExecutor
{
    public static void Execute(ReadOnlySpan<byte> param, ref CommandContext ctx)
    {
        var cmd = MemoryMarshal.Read<SetConstantBufferCommand>(param);
        IRenderDeviceV2 device =
            ctx.DeviceV2
            ?? throw new NotSupportedException(
                "SetConstantBuffer requires a device that implements IRenderDeviceV2."
            );
        device.BindConstantBuffer(cmd.Binding, cmd.Buffer, cmd.OffsetBytes, cmd.SizeBytes);
    }
}
