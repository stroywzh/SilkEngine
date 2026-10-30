using SilkEngine.Rendering.Abstraction;
using SilkEngine.Runtime.Host;

namespace SilkEngine.Runtime;

/// <summary>Creates and starts engine-owned threads.</summary>
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

    /// <summary>Creates, registers and starts an engine thread of type <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">Concrete engine thread type.</typeparam>
    /// <returns>The started thread.</returns>
    public static T Start<T>()
        where T : EngineThread, new()
    {
        var runtime = new T();
        ThreadRegistry.Register(runtime);
        runtime.Start();
        return runtime;
    }

    /// <summary>Creates, registers and starts the main loop.</summary>
    /// <returns>The started main loop.</returns>
    public static MainLoop StartMainThread()
    {
        return Start<MainLoop>();
    }

    /// <summary>Creates, registers and starts the asset worker owned by the engine.</summary>
    /// <returns>The started worker; the main loop stops and disposes it.</returns>
    internal static AssetThread StartAssetThread()
    {
        var runtime = new AssetThread();
        ThreadRegistry.Register(runtime);
        runtime.Start();
        return runtime;
    }

    /// <summary>Creates, registers and starts the render thread owned by a main loop.</summary>
    /// <param name="pipeline">Pipeline driven on the render thread; must not be initialized yet.</param>
    /// <param name="backend">Backend owner of the render thread; must not be initialized yet.</param>
    /// <returns>The started render thread; the main loop owns it and must stop and dispose it.</returns>
    internal static RenderThread StartRenderThread(IRenderPipeline pipeline, IRenderBackend backend)
    {
        var runtime = new RenderThread(pipeline, backend);
        ThreadRegistry.Register(runtime);
        runtime.Start();
        return runtime;
    }
}
