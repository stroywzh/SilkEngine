using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering;

public readonly struct RenderContext
{
    public readonly IRenderPipeline Pipeline;
}

public ref struct CommandContext
{
    public RenderContext Render;
    public IRenderHardware RHI;
}
