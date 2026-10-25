using System.Runtime.InteropServices;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.Attributes;

namespace SilkEngine.Rendering;

/// <summary>Binds a render target and clears it to a color.</summary>
/// <remarks>
/// The prototype clear command (proposal-03 §3.1): binding and clearing stay one command until the
/// render-target commands land (P3), so the recorded stream cannot clear a target it did not bind.
/// </remarks>
[RenderCommand((int)RenderCommandKind.ClearColor, typeof(ClearColorCommandExecutor))]
public readonly struct ClearColorCommand
{
    /// <summary>Target bound before the clear.</summary>
    public readonly RenderTargetHandle Target;

    /// <summary>Color the target is cleared to.</summary>
    public readonly Color Color;

    /// <summary>Creates the command.</summary>
    /// <param name="target">Target bound before the clear.</param>
    /// <param name="color">Color the target is cleared to.</param>
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
