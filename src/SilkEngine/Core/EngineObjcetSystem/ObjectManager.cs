using System;
using System.Collections.Generic;
using SilkEngine;
using SilkEngine.Runtime;

namespace SilkEngine.Core.ObjcetSystem;

public enum ObjectFlag : byte
{
    Normal = 0,
    DontDestoryOnLoad = 1,
}

internal class ObjectManager : ManagerBase<ObjectManager>
{
    private readonly List<GameObject> _pendingRemoval = new();
    private readonly List<GameObject> _pendingSnapshot = new();
    private readonly Dictionary<ObjectFlag, ObjectContainer> _objectContainerDict = new();
    private readonly Dictionary<ObjectId, GameObject> _objectIdDict = new();

    private readonly List<uint> _generations = new() { 0 };

    private readonly Stack<uint> _freeIndices = new();
    private uint _nextIndex = 1;

    #region Id

    /// <summary>
    /// 分配一个 ObjectId。调用方负责在构造失败时 ReleaseId 回滚。
    /// </summary>
    internal ObjectId AllocateId()
    {
        uint index;
        uint generation;

        if (_freeIndices.Count > 0)
        {
            // 复用：世代号必须 +1，旧句柄才会失效
            index = _freeIndices.Pop();
            generation = ++_generations[(int)index];
        }
        else
        {
            // 全新槽位：index 递增，世代从 0 开始
            index = _nextIndex++;
            generation = 0;
            _generations.Add(0); // 为新 index 占位
        }

        return ObjectId.Create(index, generation);
    }

    /// <summary>
    /// 回滚分配（构造失败时）。仅归还槽位，不回退 generation——
    /// 下次分配 ++ 即令旧句柄失效。
    /// </summary>
    internal void ReleaseId(ObjectId id)
    {
        if (id.IsNull)
            return;
        _freeIndices.Push(id.Index);
    }

    /// <summary>通过 ObjectId 反查（ID 分配闭环的必要查询）。</summary>
    internal bool TryGetById(ObjectId id, out GameObject go)
    {
        return _objectIdDict.TryGetValue(id, out go!);
    }

    #endregion

    #region GameObject管理方法
    /// <summary>
    /// 工厂：分配 ID → 构造 → 注册；任一步失败自动回滚 ID。
    /// </summary>
    internal GameObject CreateFromPrefab(GameObject? prefab = null)
    {
        var id = AllocateId();

        GameObject go;
        try
        {
            var token = new ObjectCreateToken(id, prefab);
            go = GameObject.CreateNew(token); // internal：内部构造 + Transform 初始化
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
    /// 注册一个已持有合法 InstanceId 的 GameObject。
    /// </summary>
    internal bool Register(GameObject go)
    {
        if (go is null || go.InstanceId.IsNull)
        {
            Log.Warning("Register: invalid GameObject or null ObjectId.");
            return false;
        }

        if (!_objectIdDict.TryAdd(go.InstanceId, go))
        {
            Log.Warning($"Register: duplicate ObjectId {go.InstanceId}.");
            return false;
        }

        if (!_objectContainerDict.TryGetValue(go.objectFlag, out var container))
        {
            _objectIdDict.Remove(go.InstanceId); // 回滚半成品
            Log.Warning($"Register: no container for flag {go.objectFlag}.");
            return false;
        }

        container.Add(go);
        return true;
    }

    internal void Destroy(GameObject go)
    {
        if (go is null)
            return;

        _pendingRemoval.Add(go);
    }

    internal void DestroyImmediate(GameObject go)
    {
        if (go is null)
            return;

        RemoveGameObject(go);
    }

    #endregion

    #region LifeCycle

    protected override void Initialize()
    {
        _objectContainerDict[ObjectFlag.Normal] = new ObjectContainer();
        _objectContainerDict[ObjectFlag.DontDestoryOnLoad] = new ObjectContainer();
    }

    protected override void OnUpdate()
    {
        foreach (var container in _objectContainerDict.Values)
        {
            foreach (var go in container.GameObjects)
            {
                go.UpdateInternal();
            }
        }
    }

    protected override void OnFixedUpdate()
    {
        foreach (var container in _objectContainerDict.Values)
        {
            foreach (var go in container.GameObjects)
            {
                go.FixedUpdateInternal();
            }
        }
    }

    protected override void OnLateUpdate()
    {
        // 下一帧 LateUpdate 处理上一帧的 Destroy
        if (_pendingRemoval.Count > 0)
        {
            _pendingSnapshot.AddRange(_pendingRemoval);
            _pendingRemoval.Clear();

            foreach (var go in _pendingSnapshot)
            {
                RemoveGameObject(go);
            }
            _pendingSnapshot.Clear();
        }

        foreach (var container in _objectContainerDict.Values)
        {
            foreach (var go in container.GameObjects)
            {
                go.LateUpdateInternal();
            }
        }
    }

    private void RemoveGameObject(GameObject go)
    {
        if (_objectContainerDict.TryGetValue(go.objectFlag, out var container))
        {
            container.Remove(go);
        }

        if (_objectIdDict.Remove(go.InstanceId))
        {
            _freeIndices.Push(go.InstanceId.Index);
        }
    }
    #endregion
}
