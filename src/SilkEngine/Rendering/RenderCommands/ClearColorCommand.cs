using System.Runtime.InteropServices;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.Attributes;

namespace SilkEngine.Rendering;

/// <summary>Binds a render target and clears it to a color.</summary>
[RenderCommand((int)RenderCommandKind.ClearColor, typeof(ClearColorCommandExecutor))]
public readonly struct ClearColorCommand
{
    /// <summary>Target bound before the clear.</summary>
    public readonly RenderTargetHandle Target;

    /// <summary>Color the target is cleared to.</summary>
    public readonly Color Color;

    /// <summary>Creates the command.</summary>
    public ClearColorCommand(RenderTargetHandle target, Color color)
    {
        Target = target;
        Color = color;
    }
}

internal static class ClearColorCommandExecutor
{
    public static void Execute(ReadOnlySpan<byte> param, ref CommandContext ctx)
    {
        var cmd = MemoryMarshal.Read<ClearColorCommand>(param);
        ctx.Device.BindRenderTarget(cmd.Target);
        ctx.Device.ClearColor(cmd.Color);
    }
}
