using SilkEngine.Runtime.Host;

namespace SilkEngine.Runtime;

/// <summary>
/// Thread registry of the running engine. It currently tracks the single main loop; the remaining
/// bookkeeping (thread assertions) is not implemented yet.
/// </summary>
//TODO: 这个东西现在多少有点石山了，
public static class ThreadRegistry
{
    private static MainLoop? _mainThread;
    private static AssetThread? _assetWorker;

    /// <summary>The started asset worker, for diagnostics; ownership stays with the main loop.</summary>
    internal static AssetThread? AssetWorker => Volatile.Read(ref _assetWorker);

    /// <summary>Registers a started engine thread.</summary>
    /// <param name="thread">Thread being registered.</param>
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
        else if (thread is AssetThread assetThread)
        {
            Volatile.Write(ref _assetWorker, assetThread);
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

        if (ReferenceEquals(Volatile.Read(ref _assetWorker), thread))
            Volatile.Write(ref _assetWorker, null);
    }

    /// <summary>Stops and disposes the registered main loop, if any.</summary>
    public static void StopMainThread()
    {
        _mainThread?.RequestStop();
        _mainThread?.Dispose();
        _mainThread = null;
    }
}
