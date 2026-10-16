using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering;

public abstract class CommandBufferBase : ICommandBuffer
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



    public void Dispose()
    {
        _commands.Clear();
    }
}
