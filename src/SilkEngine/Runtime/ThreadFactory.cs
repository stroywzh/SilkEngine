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
}
