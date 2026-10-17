using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.BuiltIn;

/// <summary>
/// Built-in pipeline skeleton. This milestone validates the frame protocol only and records no GPU work.
/// </summary>
public sealed class BuiltInPipeline : RenderPipelineBase
{
    /// <inheritdoc />
    public override string Name => "Built-In Pipeline";

    /// <inheritdoc />
    protected override void OnInitialize()
    {
        Log.Info("Pipeline Init");
        
    }

    /// <inheritdoc />
    protected override void RecordCommands(ICommandBuffer commands, RenderContext ctx)
    {
        commands.ClearColor(default,new System.Numerics.Vector4(1,1,1,1));
    }

    /// <inheritdoc />
    protected override void OnResize(int width, int height) { }

    /// <inheritdoc />
    protected override void DisposeCore() { }
}

