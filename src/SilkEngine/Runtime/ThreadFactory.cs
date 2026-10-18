using SilkEngine.Rendering.Abstraction;
using SilkEngine.Runtime.Host;

namespace SilkEngine.Runtime;

public static class ThreadFactory
{
    /// <summary>
    /// Create a new thread with params
    /// </summary>
    /// <param name="entry"></param>
    /// <param name="name"></param>
    /// <param name="isBackground"></param>
    /// <param name="priority"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static Thread CreateThread(
        Action entry,
        string name,
        bool isBackground = false,
        ThreadPriority priority = ThreadPriority.Normal
    )
    {
        if (entry is null)
            throw new ArgumentNullException(nameof(entry));
        return new Thread(() => entry())
        {
            Name = name,
            IsBackground = isBackground,
            Priority = priority,
        };
    }

    public static T RuntimeBuilder<T>()
        where T : RuntimeBase, new()
    {
        var runtime = new T();
        RuntimeRegistry.Register(runtime);
        runtime.Start();
        return runtime;
    }

    public static MainLoop BuildMainThread()
    {
        return RuntimeBuilder<MainLoop>();
    }

    /// <summary>Creates, registers and starts the render thread owned by a main loop.</summary>
    /// <param name="pipeline">Pipeline driven on the render thread; must not be initialized yet.</param>
    /// <param name="backend">Backend owner of the render thread; must not be initialized yet.</param>
    /// <param name="device">Graphics device handed to the backend on the render thread.</param>
    /// <returns>The started render runtime; the main loop owns it and must stop and dispose it.</returns>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    internal static RenderLoop BuildRenderThread(IRenderPipeline pipeline, IRenderBackend backend)
    {
        var runtime = new RenderLoop(pipeline, backend);
        RuntimeRegistry.Register(runtime);
        runtime.Start();
        return runtime;
    }
}
