using System;

namespace SilkEngine.Core.ObjcetSystem;

public readonly struct ObjectId : IEquatable<ObjectId>
{
    private readonly Handle<ulong> _handle;
    public ulong Value => _handle.Value;
    public uint Index => (uint)_handle.Value;
    public uint Generation => (uint)(_handle.Value >> 32);

    internal ObjectId(ulong value) => _handle = new(value);

    /// <summary>
    /// Internal factory method.
    /// </summary>
    /// <param name="index"></param>
    /// <param name="generation"></param>
    /// <returns></returns>
    internal static ObjectId Create(uint index, uint generation) =>
        new ObjectId(((ulong)generation << 32) | index);

    /// <summary>
    /// default 即无效句柄。因为 Index 从 1 起分配、0 号槽永久保留，
    /// 任何已分配的 Id 其 Value != 0，因此 Value == 0 等价于"无效"。
    /// </summary>
    public bool IsNull => Value == 0;
    public static ObjectId Null => default;

    public bool Equals(ObjectId other) => Value == other.Value;

    public override bool Equals(object? obj) => obj is ObjectId e && Value == e.Value;

    public override int GetHashCode() => Value.GetHashCode();

    public static bool operator ==(ObjectId a, ObjectId b) => a.Value == b.Value;

    public static bool operator !=(ObjectId a, ObjectId b) => a.Value != b.Value;

    public override string ToString() =>
        $"EntityId(Index={Index}, Gen={Generation}, IsNull={this.IsNull})";
}
