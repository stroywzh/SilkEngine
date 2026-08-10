using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace ProjectEngine.Threading;

public enum WorkPriority { Low, Normal, High }

public class EngineThreadPool : IDisposable
{
    private readonly ConcurrentQueue<WorkItem> _high = new();
    private readonly ConcurrentQueue<WorkItem> _normal = new();
    private readonly ConcurrentQueue<WorkItem> _low = new();
    private readonly List<Thread> _workers = new();
    private volatile bool _running;

    private struct WorkItem
    {
        public Action? Action;
        public CancellationToken Token;
    }

    public EngineThreadPool(int workerCount = 1)
    {
        _running = true;
        for (int i = 0; i < workerCount; i++)
        {
            var t = ThreadFactory.CreateThread(WorkerLoop, $"PoolWorker-{i}");
            _workers.Add(t);
            t.Start();
        }
    }

    public void EnqueueWork(Action work,
        WorkPriority priority = WorkPriority.Normal,
        CancellationToken token = default)
    {
        if (token.IsCancellationRequested) return;
        var item = new WorkItem { Action = work, Token = token };
        (priority switch
        {
            WorkPriority.High => _high,
            WorkPriority.Low => _low,
            _ => _normal
        }).Enqueue(item);
    }

    private void WorkerLoop()
    {
        var spin = new SpinWait();
        while (_running)
        {
            if (TryDequeue(out var item))
            {
                spin.Reset();
                if (!item.Token.IsCancellationRequested)
                    item.Action?.Invoke();
            }
            else
            {
                spin.SpinOnce();
                if (spin.Count > 1000) Thread.Sleep(1);
            }
        }
        while (TryDequeue(out var item))
            if (!item.Token.IsCancellationRequested)
                item.Action?.Invoke();
    }

    private bool TryDequeue(out WorkItem item) =>
        _high.TryDequeue(out item) || _normal.TryDequeue(out item) || _low.TryDequeue(out item);

    public void Shutdown()
    {
        _running = false;
        foreach (var t in _workers) t.Join();
    }

    public void Dispose() => Shutdown();
}
