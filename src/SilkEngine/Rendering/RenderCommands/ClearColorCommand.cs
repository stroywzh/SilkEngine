using System.Numerics;
using System.Runtime.InteropServices;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.Attributes;
using SilkEngine.Rendering.Internal;

namespace SilkEngine.Rendering;

// 架构预设
//
// Backend (ExecuteCommands)
//   └─ foreach entry: Dispatch(entry.Id, entry.Payload, ref ctx)
//          │（这里通过Dispatch找到源生成的Switch分发）
//          └─ ClearCommandExecutor.Execute(span, ref ctx)      ← 唯一执行点
//                 │
//                 └─ ctx.RHI.ClearColor(cmd.Target, cmd.Color);          // 执行结果
//                        │
//                        └─ SilkOpenGLBackend.IRenderHardware.ClearColor → gl.Clear(...)
// 注意，这里的注释不是最新的
// Backend.OnPresent()   ← backend 消费"执行完成"这一结果

[RenderCommand((int)RenderCommandKind.ClearColor, typeof(ClearCommandExecutor))]
public readonly struct ClearColorCommand
{
    public readonly RenderTargetHandle Target;
    public readonly Color Color;

    public readonly RenderItemSortKey Key;

    public ClearColorCommand(RenderTargetHandle target, Color color, RenderItemSortKey key)
    {
        Target = target;
        Color = color;
        Key = key;
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
