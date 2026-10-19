using System;
using System.Collections.Generic;
using SilkEngine;

namespace SilkEngine.Core.EngineObjcetSystem;

public sealed class EngineObjectManager : Singleton<EngineObjectManager>
{
    private ObjectBase[] _entities;
    private uint[] _generations;
    private bool[] _alive;
    private int _count;
    private readonly Stack<uint> _freeIndices = new();

    // 自注册机制：登记即入队，OnCreate 由 FlushActivations 在帧边界统一触发
    private readonly Queue<ObjectBase> _pendingActivation = new();

    public EngineObjectManager(int capacity = 256)
    {
        capacity = EngineMath.Max(1, capacity);
        _entities = new ObjectBase[capacity];
        _generations = new uint[capacity];
        _alive = new bool[capacity];
    }

    public int AliveCount { get; private set; }

    /// <summary>
    /// 构造即登记：分配 ID、写入槽位、入激活队列。不调任何虚方法——
    /// 因为此时派生类字段初始化器尚未执行。
    ///
    /// 前置条件：调用方运行在主线程（由 EngineObjectManager 的创建与使用约定保证）。
    /// 已知边界：若派生类字段初始化器抛异常，已分配的槽位将泄漏；
    /// 这是 bug 路径，不做回滚。
    /// </summary>
    internal void Register(ObjectBase e)
    {
        var id = AllocateId();
        e.InstanceId = id;
        _entities[id.Index] = e;
        _pendingActivation.Enqueue(e);
    }

    /// <summary>
    /// 序列化恢复专用：调用方用 RuntimeHelpers.GetUninitializedObject 跳过构造，
    /// 再手动恢复 ID，让恢复路径也收敛到同一个激活队列。
    /// </summary>
    internal void RestoreWithId(ObjectBase e, ObjectId id)
    {
        if (id.Value == 0)
            throw new ArgumentException("不能恢复 Null ID。", nameof(id));

        // 扩容以容纳恢复的 Index
        while (id.Index >= (uint)_entities.Length)
        {
            int cap = _entities.Length * 2;
            Array.Resize(ref _entities, cap);
            Array.Resize(ref _generations, cap);
            Array.Resize(ref _alive, cap);
        }

        if (_alive[id.Index])
            throw new InvalidOperationException($"槽位 {id.Index} 已被占用，ID 冲突。");
        if (_generations[id.Index] >= id.Generation)
            throw new InvalidOperationException(
                $"ID 代 {id.Generation} 不足以覆盖槽位 {id.Index} 的当前代 "
                    + $"{_generations[id.Index]}，疑似老 ID 复活。"
            );

        // 若恢复的 Index 超出已用范围，把中间的洞标记为"首次使用"并放入 freeIndices，
        // 交给后续 Create 复用；避免 AllocateId 从洞取到 Generation=0 的 Null ID。
        for (uint i = (uint)_count; i < id.Index; i++)
        {
            _generations[i] = 1;
            _freeIndices.Push(i);
        }
        if (id.Index >= (uint)_count)
            _count = (int)id.Index + 1;

        e.InstanceId = id;
        _entities[id.Index] = e;
        _alive[id.Index] = true;
        _generations[id.Index] = id.Generation;
        AliveCount++;
        _pendingActivation.Enqueue(e);
    }

    /// <summary>
    /// 帧边界统一触发 OnCreate。MainLoop 每帧调用一次。
    /// Flush 期间再 new 的对象会进下一帧队列（快照 + Clear 语义）。
    /// </summary>
    public void FlushActivations()
    {
        if (_pendingActivation.Count == 0)
            return;

        var snapshot = _pendingActivation.ToArray();
        _pendingActivation.Clear();

        foreach (var e in snapshot)
        {
            // 同一批 Flush 里被先前的 OnCreate 链式 Destroy 的可能性
            if (!IsAlive(e.InstanceId))
                continue;
            e.OnCreate();
        }
    }

    public bool Destroy(ObjectBase? e)
    {
        if (e is null)
            return false;
        if (!IsAlive(e.InstanceId))
            return false;

        uint index = e.InstanceId.Index;
        e.InstanceId = default;

        // 先反注册，后调 OnDestroyCore：
        //   OnDestroy 里 IsAlive(this) == false，语义干净；
        //   OnDestroy 抛异常也不会泄漏槽。
        _alive[index] = false;
        _entities[index] = null!;
        AliveCount--;

        _generations[index]++;
        if (_generations[index] != 0)
            _freeIndices.Push(index);

        try
        {
            e.OnDestroyCore();
        }
        catch
        {
            // 清理已完成，OnDestroyCore 的异常不向上冒泡污染调用方。
        }

        return true;
    }

    public ObjectBase? Resolve(ulong id)
    {
        if (!IsAlive(id))
            return null;
        return _entities[(uint)id];
    }

    public ObjectBase? Resolve(ObjectId id) => Resolve(id.Value);

    public bool TryResolve<T>(ulong id, out T entity)
        where T : ObjectBase
    {
        entity = null!;
        if (!IsAlive(id))
            return false;

        entity = (T)_entities[(uint)id];
        return entity is not null;
    }

    public bool TryResolve<T>(ObjectId id, out T entity)
        where T : ObjectBase => TryResolve(id.Value, out entity);

    public bool IsAlive(uint index, uint generation) =>
        index < (uint)_count && _alive[index] && _generations[index] == generation;

    public bool IsAlive(ulong id) => IsAlive((uint)id, (uint)(id >> 32));

    public bool IsAlive(ObjectId id) => IsAlive(id.Index, id.Generation);

    private ObjectId AllocateId()
    {
        uint index;
        if (_freeIndices.Count > 0)
        {
            index = _freeIndices.Pop();
        }
        else
        {
            CheckAvailable();
            index = (uint)_count++;
            _generations[index] = 1; // 首次分配代=1，ObjectId.Null 代=0 永不冲突
        }
        _alive[index] = true;
        AliveCount++;
        return ObjectId.Create(index, _generations[index]);
    }

    private void CheckAvailable()
    {
        if (_count == _entities.Length)
        {
            int cap = _entities.Length * 2;
            Array.Resize(ref _entities, cap);
            Array.Resize(ref _generations, cap);
            Array.Resize(ref _alive, cap);
        }
    }
}
