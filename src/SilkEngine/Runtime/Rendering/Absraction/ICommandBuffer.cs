using SilkEngine;

namespace SilkEngine.Runtime.Rendering.Abstraction;

public interface ICommandBuffer
{
    /// <summary>
    /// Add a new <see cref="IRenderCommand"/> to this buffer.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="command">A struct that with <see cref="IRenderCommand"/></param>
    public void AddCommand<T>(in T command)
        where T : struct, IRenderCommand;

    /// <summary>
    /// Use this to execute all commands in this buffer.
    /// </summary>
    /// <param name="ctx"></param>
    void Execute(RenderContext ctx);

    /// <summary>
    /// Reset this buffer to default state.
    /// <br/>Use this before next <see cref="Execute(RenderContext)"/> call.
    /// </summary>
    void Reset();
}
