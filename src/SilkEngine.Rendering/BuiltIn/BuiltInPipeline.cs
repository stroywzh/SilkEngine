using System.Numerics;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.BuiltIn;

/// <summary>Explicit prototype target registration shared by the built-in pipeline and GL backend.</summary>
public static class BuiltInTargets
{
    /// <summary>Engine target id mapped explicitly to this prototype's window default framebuffer.</summary>
    public const ushort Window = 1;
}

/// <summary>Prototype pipeline that records a time-varying clear color for the window target.</summary>
public sealed class BuiltInPipeline : RenderPipelineBase
{
    private double _elapsedSeconds;

    public override string Name => "Built-In Pipeline";

    protected override void OnInitialize() { }

    protected override void RecordCommands(ICommandBuffer commands, RenderContext context)
    {
        _elapsedSeconds = (_elapsedSeconds + Time.UnscaledDeltaTime) % (Math.PI * 2);
        var t = _elapsedSeconds;
        var color = new Vector4(
            (float)(0.5 + 0.5 * Math.Sin(t)),
            (float)(0.5 + 0.5 * Math.Sin(t + 2)),
            (float)(0.5 + 0.5 * Math.Sin(t + 4)),
            1
        );
        commands.ClearColor(new RenderTargetHandle(BuiltInTargets.Window), color);
    }
}
