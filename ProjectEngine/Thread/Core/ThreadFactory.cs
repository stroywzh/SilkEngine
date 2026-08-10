using System;
using System.Threading;

namespace ProjectEngine.Threading;

public static class ThreadFactory
{
    public static Thread CreateThread(
        Action loop,
        string name,
        bool isBackground = true,
        ThreadPriority priority = ThreadPriority.Normal
    )
    {
#if DEBUG
        Log.Info(
            $"[ThreadFactory] Creating New Thread:[{name}]|Background:{isBackground}|Priority:{priority}"
        );
#endif
        return new Thread(() => loop())
        {
            Name = name,
            IsBackground = isBackground,
            Priority = priority,
        };
    }
}
