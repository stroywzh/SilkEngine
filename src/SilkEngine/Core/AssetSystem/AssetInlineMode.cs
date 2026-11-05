namespace SilkEngine.Core.AssetSystem;

/// <summary>
/// Test/tool mode: run asset requests inline on the calling thread instead of queueing them for the worker.
/// </summary>
/// <remarks>Only tests and single-threaded tools may enter this mode; the engine never does. Scopes nest through a
/// counter, so independent test classes cannot leave the mode half-enabled.</remarks>
internal static class AssetInlineMode
{
    private static int s_depth;

    internal static bool IsActive => Volatile.Read(ref s_depth) > 0;

    internal static IDisposable Enter()
    {
        Interlocked.Increment(ref s_depth);
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            Interlocked.Decrement(ref s_depth);
        }
    }
}
