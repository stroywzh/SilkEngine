using System.Runtime.InteropServices;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.Attributes;

namespace SilkEngine.Rendering;

/// <summary>Applies a pipeline state.</summary>
[RenderCommand((int)RenderCommandKind.SetPipeline, typeof(SetPipelineStateExecutor))]
public readonly struct SetPipelineStateCommand
{
    /// <summary>Pipeline state to apply.</summary>
    public readonly PipelineStateHandle PipelineState;

    public SetPipelineStateCommand(PipelineStateHandle pipelineState)
    {
        PipelineState = pipelineState;
    }
}

internal static class SetPipelineStateExecutor
{
    public static void Execute(ReadOnlySpan<byte> param, ref CommandContext ctx)
    {
        var cmd = MemoryMarshal.Read<SetPipelineStateCommand>(param);
        ctx.Device.SetPipelineState(cmd.PipelineState);
    }
}
