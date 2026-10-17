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
    protected override void OnInitialize() { }

    /// <inheritdoc />
    protected override void RecordCommands(ICommandBuffer commands, RenderContext ctx) { }

    /// <inheritdoc />
    protected override void OnResize(int width, int height) { }

    /// <inheritdoc />
    protected override void DisposeCore() { }
}

