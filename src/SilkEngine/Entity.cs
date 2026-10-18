namespace SilkEngine;

using System;

public readonly struct EntityId : IEquatable<EntityId>
{
    public readonly ulong Value;

    public uint Index => (uint)Value;
    public uint Generation => (uint)(Value >> 32);

    internal EntityId(ulong value) => Value = value;

    /// <summary>
    /// Internal factory method.
    /// </summary>
    /// <param name="index"></param>
    /// <param name="generation"></param>
    /// <returns></returns>
    internal static EntityId Create(uint index, uint generation) =>
        new EntityId(((ulong)generation << 32) | index);

    /// <summary>default 即无效句柄（要求 Generation 从 1 起分配）。</summary>
    public bool IsNull => Value == 0;
    public static EntityId Null => default;

    public bool Equals(EntityId other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is EntityId e && Value == e.Value;

    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(EntityId a, EntityId b) => a.Value == b.Value;

    public static bool operator !=(EntityId a, EntityId b) => a.Value != b.Value;

    public override string ToString() => $"EntityId(Index={Index}, Gen={Generation})";
}

public abstract class Entity
{
    /// <summary>当前实体的句柄。销毁后保持不变，但通过 Manager 校验会失败。</summary>
    public EntityId Id { get; internal set; }

    /// <summary>是否已被 Destroy。一旦为 true 永久为 true。</summary>
    public bool IsDestroyed { get; private set; }

    protected internal virtual void OnCreate() { }

    protected internal virtual void OnEnable() { } // 可选：对象池复用时回调

    protected internal virtual void OnDisable() { } // 可选

    protected internal virtual void OnDestroy() { }

    // public static Entity Instantiate()
    // {
    //     return 
    // }

    /// <summary>
    /// 内部使用：由 Manager 调用，禁止外部调用。
    /// </summary>
    internal void MarkDestroyed()
    {
        if (IsDestroyed)
            return;
        OnDisable();
        OnDestroy();
        IsDestroyed = true;
    }
}
