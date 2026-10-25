using System;
using System.Threading;

namespace SilkEngine.Runtime;

/// <summary>Lifecycle state of an <see cref="EngineThread"/>; states only move forward.</summary>
public enum EngineThreadState : byte
{
    /// <summary>Constructed, not started yet.</summary>
    Created = 0,

    /// <summary>Start requested; the OS thread may not have entered the loop yet.</summary>
    Starting = 1,

    /// <summary>Loop body is running.</summary>
    Running = 2,

    /// <summary>Stop requested; the loop is winding down.</summary>
    Stopping = 3,

    /// <summary>Disposed; the loop has finished and resources are released.</summary>
    Disposed = 4,
}

/// <summary>
/// Base class for every engine-owned thread (main loop and render thread).
/// </summary>
/// <remarks>
/// State transitions are monotonic and thread-safe; starting twice will throws.
/// </remarks>
public abstract class EngineThread : IDisposable
{
    private Thread? _thread;
    private readonly CancellationTokenSource _cts = new();
    private int _state = (int)EngineThreadState.Created;

    /// <summary>Whether the owned OS thread is still alive.</summary>
    public bool IsAlive => _thread?.IsAlive ?? false;

    /// <summary>Current lifecycle state.</summary>
    public EngineThreadState State => (EngineThreadState)Volatile.Read(ref _state);

    /// <summary>Token cancelled when the thread is asked to stop.</summary>
    public CancellationToken StopToken => _cts.Token;

    /// <summary>
    /// Override this to change Thread's name.
    /// </summary>
    public virtual string ThreadName => GetType().Name;

    /// <summary>
    /// Override this to change Thread's background prop.
    /// </summary>
    public virtual bool IsBackground => true;

    /// <summary>
    /// IsThisMainThread
    /// </summary>
    internal virtual bool IsMainThread => false;

    /// <summary>
    /// Start the host thread
    /// </summary>
    /// <exception cref="EngineThreadStateException">宿主已启动过。</exception>
    public void Start()
    {
        if (!TryEnter(EngineThreadState.Starting))
            throw new EngineThreadStateException($"{ThreadName} has already been started.");

        Log.Info("Entering EngineThreadState Starting...");
        var token = StopToken;
        _thread = ThreadFactory.CreateThread(() => RunInternal(token), ThreadName, IsBackground);
        _thread.Start();
        Log.Info($"-Thread:{ThreadName},MID:{_thread.ManagedThreadId} -- Invoke Start()");
    }

    private void RunInternal(CancellationToken token)
    {
        try
        {
            token.ThrowIfCancellationRequested();
            if (!TryEnter(EngineThreadState.Running))
            {
                token.ThrowIfCancellationRequested();
                throw new EngineThreadStateException(
                    $"{ThreadName} cannot enter EngineThreadState.Running."
                );
            }
            token.ThrowIfCancellationRequested();

            // C-14/C-09 + CORE-N-05: only the main thread reflects the engine-wide running flag; a
            // render thread (or any other engine thread) must never clear or set it.
            if (IsMainThread)
            {
                Application.IsRunning = true;
            }

            Initialize();

            while (ShouldUpdate(token))
            {
                Run();
            }
        }
        catch (OperationCanceledException error)
            when (token.IsCancellationRequested && error.CancellationToken == token)
        {
            Log.Info($"{ThreadName} normal exit");
        }
        catch (Exception ex)
        {
            Log.Exception(ex);
        }
        finally
        {
            try
            {
                Shutdown();
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
            }
        }
    }

    /// <summary>
    /// Initialize logic, excute once before <see cref="Run"/>
    /// </summary>
    protected virtual void Initialize() { }

    /// <summary>
    /// Thread workload-loop.
    /// </summary>
    protected abstract void Run();

    /// <summary>
    /// Called when thread shutdown, excute once after <see cref="Run"/>, no matter normal exit or exception happend.
    /// </summary>
    protected virtual void Shutdown() { }

    /// <summary>Whether the loop should keep running.</summary>
    protected virtual bool ShouldUpdate(CancellationToken token)
    {
        return !token.IsCancellationRequested;
    }

    /// <summary>
    /// Stop workLoad.
    /// </summary>
    public virtual void RequestStop()
    {
        if (!TryEnter(EngineThreadState.Stopping))
            return;
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException) { }
        finally
        {
            Log.Info($"{ThreadName} Request stop.");
        }
    }

    /// <summary>Stops the loop, waits for the thread to exit and releases the thread resources.</summary>
    public void Dispose()
    {
        RequestStop();

        if (!TryEnter(EngineThreadState.Disposed))
            return;

        // a render/worker thread being disposed must not report the engine as stopped.
        if (IsMainThread)
        {
            Application.IsRunning = false;
        }

        try
        {
            DisposeCore();
        }
        finally
        {
            try
            {
                WaitForExit();
            }
            finally
            {
                _cts.Dispose();
            }
        }
        Log.Info($"{ThreadName} Disposed.");
    }

    /// <summary>
    /// Called after RequestStop at Dispose,use this to dispose res.
    /// </summary>
    protected virtual void DisposeCore() { }

    private void WaitForExit()
    {
        var thread = _thread;
        if (thread is null || !thread.IsAlive || thread == Thread.CurrentThread)
            return;

        thread.Join();
    }

    /// <summary>
    /// Try enter any state if avalible.
    /// <br/>return false if target state is unavailable or currently already in.
    /// </summary>
    /// <param name="target">TargetState</param>
    protected bool TryEnter(EngineThreadState target) => TryEnter((int)target);

    /// <summary>
    /// Try enter any state if avalible.
    /// <br/>return false if target state is unavailable or currently already in.
    /// </summary>
    /// <param name="target">TargetState</param>
    /// <returns>true if success</returns>
    protected bool TryEnter(int target)
    {
        if (target <= (int)EngineThreadState.Created || target > (int)EngineThreadState.Disposed)
            return false;

        while (true)
        {
            int current = Volatile.Read(ref _state);
            if (current >= target)
                return false;
            if (Interlocked.CompareExchange(ref _state, target, current) == current)
                return true;
        }
    }
}
