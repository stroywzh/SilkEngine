using SilkEngine.Runtime.Host;

namespace SilkEngine.Runtime;

/// <summary>
/// Thread registry of the running engine. It currently tracks the single main loop; the remaining
/// bookkeeping (thread assertions) is not implemented yet.
/// </summary>
public static class ThreadRegistry
{
    private static MainLoop? _mainThread;

    /// <summary>Registers a started engine thread.</summary>
    /// <param name="thread">Thread being registered.</param>
    /// <exception cref="ArgumentException">A second main thread is registered.</exception>
    public static void Register(EngineThread thread)
    {
        ArgumentNullException.ThrowIfNull(thread);

        if (thread.IsMainThread && thread is MainLoop mainLoop)
        {
            if (_mainThread is not null)
            {
                throw new ArgumentException("不能有两个主线程");
            }
            _mainThread = mainLoop;
        }
    }

    /// <summary>Removes a thread from the registry; unknown threads are ignored.</summary>
    /// <param name="thread">Thread being removed.</param>
    public static void Unregister(EngineThread thread)
    {
        if (thread is null)
            return;

        if (ReferenceEquals(_mainThread, thread))
            _mainThread = null;
    }

    /// <summary>Stops and disposes the registered main loop, if any.</summary>
    public static void StopMainThread()
    {
        _mainThread?.RequestStop();
        _mainThread?.Dispose();
        _mainThread = null;
    }
}
