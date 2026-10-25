using System.Runtime.InteropServices;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.Attributes;

namespace SilkEngine.Rendering;

/// <summary>Binds a texture to a texture slot.</summary>
[RenderCommand((int)RenderCommandKind.BindTexture, typeof(BindTextureExecutor))]
public readonly struct BindTextureCommand
{
    /// <summary>Texture to bind.</summary>
    public readonly TextureHandle Texture;

    /// <summary>Zero-based texture slot.</summary>
    public readonly uint Slot;

    /// <summary>Creates the command.</summary>
    /// <param name="texture">Texture to bind.</param>
    /// <param name="slot">Zero-based texture slot.</param>
    public BindTextureCommand(TextureHandle texture, uint slot)
    {
        Texture = texture;
        Slot = slot;
    }
}

internal static class BindTextureExecutor
{
    public static void Execute(ReadOnlySpan<byte> param, ref CommandContext ctx)
    {
        var cmd = MemoryMarshal.Read<BindTextureCommand>(param);
        ctx.Device.BindTexture(cmd.Slot, cmd.Texture);
    }
}
