using System.Runtime.InteropServices;
using SilkEngine.Rendering.Attributes;

namespace SilkEngine.Rendering;

[RenderCommand((int)RenderCommandKind.ClearColor, typeof(ClearCommandExecutor))]
public readonly struct ClearColorCommand
{
    public readonly RenderTargetHandle Target;
    public readonly Color Color;

    public ClearColorCommand(RenderTargetHandle target, Color color)
    {
        Target = target;
        Color = color;
    }
}

public static class ClearCommandExecutor
{
    public static void Execute(ReadOnlySpan<byte> param, ref CommandContext ctx)
    {
        var cmd = MemoryMarshal.Read<ClearColorCommand>(param);
        ctx.RHI.BindRenderTarget(cmd.Target);
        ctx.RHI.ClearColor(cmd.Color);
    }
}
