using System.Collections.Concurrent;

namespace SilkEngine.Core.LogSystem;

/// <summary>
/// Bounded hand-off between log producers and the worker thread that owns file IO.
/// </summary>
/// <remarks>
/// Producers only enqueue: no IO and no waiting, so a log burst from a game thread cannot stall it. A full
/// queue drops the newest line and counts it instead of blocking. The sink writes directly while no owner is
/// attached (startup and shutdown) or when the caller already is the owner, which keeps the worker from
/// waiting on itself.
/// </remarks>
internal static class LogIoQueue
{
    /// <summary>Maximum number of queued lines; further lines are dropped and counted.</summary>
    internal const int Capacity = 16384;

    /// <summary>Lines the owner writes per loop iteration, so a burst cannot delay its own work.</summary>
    internal const int DrainBudget = 256;

    private static readonly ConcurrentQueue<string> s_lines = new();
    private static int s_count;
    private static long s_dropped;
    private static int s_ownerThreadId;

    /// <summary>Queued line count (diagnostics).</summary>
    internal static int Count => Volatile.Read(ref s_count);

    /// <summary>Lines dropped because the queue was full (diagnostics).</summary>
    internal static long DroppedLines => Interlocked.Read(ref s_dropped);

    /// <summary>True while a worker owns file IO; false before startup and after shutdown.</summary>
    internal static bool HasOwner => Volatile.Read(ref s_ownerThreadId) != 0;

    /// <summary>True when the calling thread is the IO owner.</summary>
    internal static bool IsOwnerThread =>
        Volatile.Read(ref s_ownerThreadId) == Environment.CurrentManagedThreadId
        && Volatile.Read(ref s_ownerThreadId) != 0;

    /// <summary>Makes the calling thread the IO owner (called once when the worker starts).</summary>
    internal static void AttachOwner() =>
        Volatile.Write(ref s_ownerThreadId, Environment.CurrentManagedThreadId);

    /// <summary>Releases ownership so later records fall back to direct writes.</summary>
    internal static void DetachOwner() => Volatile.Write(ref s_ownerThreadId, 0);

    /// <summary>Queues one formatted line; drops it and bumps the counter when the queue is full.</summary>
    internal static void Enqueue(string line)
    {
        if (Interlocked.Increment(ref s_count) > Capacity)
        {
            Interlocked.Decrement(ref s_count);
            Interlocked.Increment(ref s_dropped);
            return;
        }

        s_lines.Enqueue(line);
    }

    /// <summary>Removes one queued line.</summary>
    internal static bool TryDequeue(out string line)
    {
        if (s_lines.TryDequeue(out line!))
        {
            Interlocked.Decrement(ref s_count);
            return true;
        }

        line = string.Empty;
        return false;
    }
}
