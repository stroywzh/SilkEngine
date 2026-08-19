using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SilkEngine.Core;

namespace SilkEngine.Threading;

public class EngineThreadPool : IWorkerScheduler, IDisposable
{
    private readonly ConcurrentQueue<WorkItem> _high = new();
    private readonly ConcurrentQueue<WorkItem> _normal = new();
    private readonly ConcurrentQueue<WorkItem> _low = new();
    private readonly List<Thread> _workers = new();
    private volatile bool _running;

    public int WorkerThreadCount => _workers.Count;

    public int TotalWorkItemCount =>
        HighLevelWorkItemCount + NormalLevelWorkItemCount + LowLevelWorkItemCount;

    public int HighLevelWorkItemCount => _high.Count;
    public int NormalLevelWorkItemCount => _normal.Count;
    public int LowLevelWorkItemCount => _low.Count;

    private struct WorkItem
    {
        public Func<Task>? Work;
        public CancellationToken Token;
    }

    public EngineThreadPool(int workerCount = 3)
    {
        _running = true;
        for (int i = 0; i < workerCount; i++)
        {
            var t = ThreadFactory.CreateThread(WorkerLoop, $"PoolWorker-{i}");
            _workers.Add(t);
            t.Start();
        }
    }

    public void EnqueueWork(
        Func<Task> work,
        WorkPriority priority = WorkPriority.Normal,
        CancellationToken token = default
    )
    {
        if (!_running)
        {
            Log.Warn("[EngineThreadPool] EnqueueWork ignored: pool is shut down");
            return;
        }
        if (token.IsCancellationRequested)
            return;
        var item = new WorkItem { Work = work, Token = token };
        (
            priority switch
            {
                WorkPriority.High => _high,
                WorkPriority.Low => _low,
                _ => _normal,
            }
        ).Enqueue(item);
    }

    public void Schedule(
        Func<Task> work,
        WorkPriority priority = WorkPriority.Normal,
        CancellationToken ct = default
    ) => EnqueueWork(work, priority, ct);

    private void WorkerLoop()
    {
        var spin = new SpinWait();
        while (_running)
        {
            if (TryDequeue(out var item))
            {
                spin.Reset();
                if (!item.Token.IsCancellationRequested)
                {
                    try
                    {
                        item.Work?.Invoke().GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[PoolWorker] Task failed: {ex}");
                    }
                }
            }
            else
            {
                spin.SpinOnce();
                if (spin.Count > 1000)
                    Thread.Sleep(1);
            }
        }
        while (TryDequeue(out var item))
            if (!item.Token.IsCancellationRequested)
                try
                {
                    item.Work?.Invoke().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Log.Error($"[PoolWorker] Drain failed: {ex}");
                }
    }

    private bool TryDequeue(out WorkItem item) =>
        _high.TryDequeue(out item) || _normal.TryDequeue(out item) || _low.TryDequeue(out item);

    public void Shutdown()
    {
        _running = false;
        foreach (var t in _workers)
            t.Join();
    }

    public void Dispose() => Shutdown();
}
