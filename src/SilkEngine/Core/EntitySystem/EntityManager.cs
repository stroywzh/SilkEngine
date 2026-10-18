using System;
using System.Collections.Generic;
using SilkEngine;

namespace SilkEngine.Core.EntitySystem;

public sealed class EntityManager : Singleton<EntityManager>
{
    // private static EntityManager _instance;
    // internal static EntityManager Instance => _instance;
    private Entity[] _entities;
    private uint[] _generations;
    private bool[] _alive;
    private int _count;
    private readonly Stack<uint> _freeIndices = new();
    private readonly Dictionary<Type, Stack<Entity>> _pool = new();

    public EntityManager(int capacity = 256)
    {
        capacity = Math.Max(1, capacity);
        _entities = new Entity[capacity];
        _generations = new uint[capacity];
        _alive = new bool[capacity];
        // _instance = this;
    }

    public int AliveCount { get; private set; }

    public T Create<T>()
        where T : Entity, new()
    {
        var id = AllocateId();
        var e = Rent<T>();

        _entities[id.Index] = e;
        return e;
    }

    public bool Destroy(Entity? e)
    {
        if (e is null || e.IsDestroyed)
            return false;
        if (!IsAlive(e.Id))
            return false;

        uint index = e.Id.Index;
        _alive[index] = false;
        _entities[index] = null!;
        AliveCount--;

        // 代递增，使所有旧 EntityId 失效
        _generations[index]++;
        bool wrap = _generations[index] == 0; // 理论极限，2^32 次复用

        if (!wrap)
        {
            _freeIndices.Push(index);
        }

        e.MarkDestroyed();
        ReturnToPool(e);
        return true;
    }

    public Entity? Resolve(EntityId id)
    {
        if (!IsAlive(id))
            return null;

        return _entities[id.Index];
    }

    public bool TryResolve<T>(EntityId id, out T? entity)
        where T : Entity
    {
        entity = null;
        if (!IsAlive(id))
            return false;

        entity = _entities[id.Index] as T;
        return entity is not null;
    }

    public bool TryResolve<T>(EntityId id, out T? entity, out EntityId actualId)
        where T : Entity
    {
        actualId = default;
        if (!IsAlive(id))
        {
            entity = null;
            return false;
        }
        entity = _entities[id.Index] as T;
        actualId = id;
        return entity is not null;
    }

    public bool IsAlive(EntityId id)
    {
        uint idx = id.Index;
        return idx < (uint)_count && _alive[idx] && _generations[idx] == id.Generation;
    }

    private EntityId AllocateId()
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
            _generations[index] = 1; // 首次分配代=1，EntityId.Null 代=0 永不冲突
        }
        _alive[index] = true;
        AliveCount++;
        return EntityId.Create(index, _generations[index]);
    }

    private T Rent<T>()
        where T : Entity, new()
    {
        if (_pool.TryGetValue(typeof(T), out var stack) && stack.Count > 0)
            return (T)stack.Pop();
        return new T();
    }

    private void ReturnToPool(Entity e)
    {
        var t = e.GetType();
        if (!_pool.TryGetValue(t, out var stack))
            _pool[t] = stack = new Stack<Entity>();
        stack.Push(e);
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
