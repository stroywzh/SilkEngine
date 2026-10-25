using System;
using System.Collections.Generic;
using SilkEngine.Scripting;

namespace SilkEngine.Core.ObjectSystem;

/// <summary>Queues deferred destructions and owns the frame-end removal step.</summary>
/// <remarks>
/// Destruction is deferred so the dispatch loops never mutate the containers they iterate: the one
/// queued entity leaves the store at the next removal pass.
/// </remarks>
internal sealed class ObjectLifecycle
{
    private readonly List<GameObject> _pendingRemoval = new();
    private readonly List<GameObject> _pendingSnapshot = new();

    /// <summary>Schedules an entity for destruction at the next removal pass.</summary>
    internal void ScheduleDestroy(GameObject go) => _pendingRemoval.Add(go);

    /// <summary>Runs every queued removal through <paramref name="remove"/> in schedule order.</summary>
    /// <param name="remove">Removal step supplied by the manager (store + container + id release).</param>
    internal void ProcessPendingRemovals(Action<GameObject> remove)
    {
        ArgumentNullException.ThrowIfNull(remove);

        if (_pendingRemoval.Count == 0)
            return;

        _pendingSnapshot.AddRange(_pendingRemoval);
        _pendingRemoval.Clear();

        foreach (var go in _pendingSnapshot)
        {
            remove(go);
        }

        _pendingSnapshot.Clear();
    }
}
