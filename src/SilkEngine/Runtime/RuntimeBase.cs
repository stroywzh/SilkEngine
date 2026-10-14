using System;
using System.Threading;
using SilkEngine.Core;

namespace SilkEngine.Runtime;

public enum RuntimeStage : byte
{
    Create = 0,
    Start = 1,
    Running = 2,
    Stop = 3,
    Dispose = 4,
}

/// <summary>
/// Base class for all runtime thread
/// </summary>
public abstract class RuntimeBase : IDisposable
{
    protected Thread? _thread;
    private readonly CancellationTokenSource _cts = new();
    protected int _state = (int)RuntimeStage.Create;

    public bool IsAlive => _thread?.IsAlive ?? false;

    /// <summary>
    /// ThreadRuntimeStage
    /// </summary>
    public RuntimeStage RuntimeStage
    {
        get => (RuntimeStage)Volatile.Read(ref _state);
    }
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
    /// <exception cref="InvalidOperationException">宿主已启动过。</exception>
    public void Start()
    {
        if (!TryEnter(RuntimeStage.Start))
            throw new InvalidOperationException($"{ThreadName} has already been started.");

        // Not Main thread, start new thread itself.
        if (!IsMainThread)
        {
            _thread = ThreadFactory.CreateThread(
                () => RunInternal(StopToken),
                ThreadName,
                IsBackground
            );
            _thread.Start();
        }
    }

    private void RunInternal(CancellationToken token)
    {
        try
        {
            Initialize();
            Run(token);
        }
        catch (OperationCanceledException)
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
    protected abstract void Run(CancellationToken token);

    /// <summary>
    /// Called when thread shutdown, excute once after <see cref="Run"/>, no matter normal exit or exception happend.
    /// </summary>
    protected virtual void Shutdown() { }

    /// <summary>
    /// Stop workLoad.
    /// </summary>
    public void RequestStop()
    {
        if (!TryEnter(RuntimeStage.Stop))
            return;
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException) { }
    }

    public void Dispose()
    {
        RequestStop();

        if (!TryEnter(RuntimeStage.Dispose))
            return;

        DisposeCore();
        WaitForExit();
        _cts.Dispose();
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
    protected bool TryEnter(RuntimeStage target) => TryEnter((int)target);

    /// <summary>
    /// Try enter any state if avalible.
    /// <br/>return false if target state is unavailable or currently already in.
    /// </summary>
    /// <param name="target">TargetState</param>
    /// <returns>true if success</returns>
    protected bool TryEnter(int target)
    {
        if (target <= (int)RuntimeStage.Create || target > (int)RuntimeStage.Dispose)
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

    /// <summary>
    /// Try enter next state if avalible
    /// </summary>
    protected bool TryAdvance() => TryEnter(Volatile.Read(ref _state) + 1);
}
