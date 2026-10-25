using System;
using System.Collections.Generic;
using SilkEngine.Scripting;

namespace SilkEngine.Core.ObjectSystem;

/// <summary>
/// Coordinator of the object system: owns the id space (<see cref="ObjectStore"/>), the scope →
/// container map (<see cref="ObjectContainerRegistry"/>) and deferred destruction
/// (<see cref="ObjectLifecycle"/>).
/// </summary>
/// <remarks>
/// Each component owns exactly one invariant; this type only wires them together, so the dispatch
/// loops stay a single line per phase.
/// </remarks>
internal class ObjectManager : ManagerBase<ObjectManager>
{
    private readonly ObjectStore _store = new();
    private readonly ObjectContainerRegistry _containers = new();
    private readonly ObjectLifecycle _lifecycle = new();

    #region Id

    /// <summary>Allocates an <see cref="ObjectId"/>; release it when construction fails.</summary>
    internal ObjectId AllocateId() => _store.AllocateId();

    /// <summary>Rolls an allocation back (construction failed).</summary>
    internal void ReleaseId(ObjectId id) => _store.ReleaseId(id);

    /// <summary>Looks an entity up by id.</summary>
    internal bool TryGet(ObjectId id, out GameObject go) => _store.TryGet(id, out go);

    #endregion

    #region GameObject

    /// <summary>
    /// Factory Method: allocate an id → construct → register; any failing step rolls the id back.
    /// </summary>
    /// <returns>The registered entity.</returns>
    /// <remarks>The lifetime scope is fixed at allocation time; copy semantics live in
    /// <see cref="SilkEngine.Scripting.GameObject.Create"/>.</remarks>
    /// <exception cref="InvalidOperationException">The new entity could not be registered.</exception>
    internal GameObject Create()
    {
        var id = AllocateId();

        GameObject go;
        try
        {
            var token = new ObjectCreateToken(id);
            go = new GameObject(token); // internal：内部构造 + Transform 初始化
        }
        catch
        {
            ReleaseId(id);
            throw;
        }

        if (!Register(go))
        {
            ReleaseId(id);
            throw new InvalidOperationException($"Failed to register GameObject with id {id}.");
        }

        return go;
    }

    /// <summary>
    /// Registers an entity that already owns a valid <see cref="ObjectId"/>.
    /// </summary>
    /// <returns><c>true</c> when the id was free and the entity entered its scope container.</returns>
    internal bool Register(GameObject go)
    {
        if (go is null || go.Id.IsNone)
        {
            Log.Warning("Register: invalid GameObject or null ObjectId.");
            return false;
        }

        if (!_store.Add(go))
        {
            Log.Warning($"Register: duplicate ObjectId {go.Id}.");
            return false;
        }

        if (!_containers.TryGet(go.Scope, out var container))
        {
            _store.Remove(go.Id); // 回滚半成品
            Log.Warning($"Register: no container for scope {go.Scope}.");
            return false;
        }

        if (!container.Add(go))
        {
            _store.Remove(go.Id);
            Log.Warning($"Register: duplicate GameObject in scope {go.Scope}.");
            return false;
        }

        return true;
    }

    /// <summary>Schedules the entity for destruction at the next removal pass.</summary>
    /// <param name="go">Entity to destroy; <c>null</c> is ignored.</param>
    internal void Destroy(GameObject go)
    {
        if (go is null)
            return;

        _lifecycle.ScheduleDestroy(go);
    }

    /// <summary>Destroys the entity immediately, without waiting for the removal pass.</summary>
    /// <param name="go">Entity to destroy; <c>null</c> is ignored.</param>
    internal void DestroyImmediate(GameObject go)
    {
        if (go is null)
            return;

        RemoveGameObject(go);
    }

    #endregion

    #region Traversal

    /// <summary>Runs <paramref name="step"/> for every registered entity, container by container.</summary>
    /// <param name="step">Step to run.</param>
    /// <remarks>
    /// The containers are iterated through their snapshot view and by index, so a step that destroys an
    /// entity during dispatch cannot invalidate the iteration (destruction is deferred anyway).
    /// </remarks>
    internal void ForEachObject(Action<GameObject> step)
    {
        ArgumentNullException.ThrowIfNull(step);

        foreach (var container in _containers.Values)
        {
            var objects = container.GameObjects;
            for (var i = 0; i < objects.Count; i++)
            {
                step(objects[i]);
            }
        }
    }

    #endregion

    #region LifeCycle

    protected override void Initialize()
    {
        _containers.Register(ObjectScope.Default, new ObjectContainer());
        _containers.Register(ObjectScope.Persistent, new ObjectContainer());
    }

    protected override void OnUpdate() => ForEachObject(static go => go.UpdateInternal());

    protected override void OnFixedUpdate() => ForEachObject(static go => go.FixedUpdateInternal());

    protected override void OnLateUpdate()
    {
        // 下一帧 LateUpdate 处理上一帧的 Destroy
        _lifecycle.ProcessPendingRemovals(RemoveGameObject);

        ForEachObject(static go => go.LateUpdateInternal());
    }

    private void RemoveGameObject(GameObject go)
    {
        // Components receive OnDestroy before the entity leaves the container/store (CORE-N-02); the
        // notification is idempotent, so DestroyImmediate after a RemoveComponent cannot double-fire.
        go.DestroyComponents();

        if (_containers.TryGet(go.Scope, out var container))
        {
            container.Remove(go);
        }

        if (_store.Remove(go.Id))
        {
            _store.ReleaseId(go.Id);
        }
    }

    #endregion
}
