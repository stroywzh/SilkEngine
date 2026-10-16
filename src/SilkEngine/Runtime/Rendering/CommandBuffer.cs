using SilkEngine.Runtime.Rendering.Abstraction;

namespace SilkEngine.Runtime.Rendering;

public abstract class CommandBuffer : ICommandBuffer
{
    private readonly List<IRenderCommand> _commands = new();

    //<interhoic/>
    public virtual void Execute(RenderContext ctx)
    {
        foreach (var c in _commands)
        {
            c.Execute(ctx);
        }
    }

    /// <summary>
    /// Fast method that auto reset this buffer after <see cref="Execute(RenderContext)"/> has been called.
    /// </summary>
    /// <param name="ctx"></param>
    public virtual void ExcuteAndReset(RenderContext ctx)
    {
        Execute(ctx);
        Reset();
    }

    //<interhoic/>
    public virtual void Reset()
    {
        _commands.Clear();
    }

    //<interhoic/>
    public virtual void AddCommand<T>(in T command)
        where T : struct, IRenderCommand
    {
        _commands.Add(command);
    }

    public virtual void SetRenderTarget(in RenderTargetHandle target) =>
        AddCommand(new SetRenderTargetCommand(target));

    public virtual void DrawMesh(MeshHandle m, in Matrix4x4 mat, MaterialHandle matl) =>
        AddCommand(new DrawMeshCommand(m, mat, matl));
}
