using System;
using System.Diagnostics;

namespace SilkEngine;

/// <summary>
/// Identity of an engine object: a 32-bit slot index (low half) plus a 32-bit generation counter
/// (high half). The generation invalidates handles of recycled slots.
/// </summary>
public readonly struct ObjectId : IEquatable<ObjectId>
{
    private readonly ulong _value;

    /// <summary>Raw packed value: low 32 bits = <see cref="Index"/>, high 32 bits = <see cref="Generation"/>.</summary>
    public ulong Value => _value;

    /// <summary>Slot index; 0 is reserved for the invalid id.</summary>
    public uint Index => (uint)_value;

    /// <summary>Generation counter of the slot; incremented whenever the slot is reused.</summary>
    public uint Generation => (uint)(_value >> 32);

    private ObjectId(ulong value) => _value = value;

    /// <summary>
    /// Packs a slot index and a generation counter into an id.
    /// </summary>
    /// <param name="index">1-based slot index; 0 is reserved for <see cref="None"/>.</param>
    /// <param name="generation">Generation counter of that slot.</param>
    /// <returns>The packed id.</returns>
    internal static ObjectId Create(uint index, uint generation)
    {
        Debug.Assert(index != 0, "ObjectId index 0 is reserved for the invalid id.");
        return new ObjectId(((ulong)generation << 32) | index);
    }

    /// <summary>Gets the invalid id (index 0, generation 0).</summary>
    /// <remarks>
    /// A struct cannot be <c>null</c>, so "no object" is expressed by this value instead.
    /// </remarks>
    public static ObjectId None => default;

    /// <summary>Gets whether this is the invalid id.</summary>
    /// <remarks>
    /// Slots are allocated from index 1 and index 0 is never reused, so any allocated id has
    /// <c>Value != 0</c>; <c>Value == 0</c> therefore means "invalid".
    /// </remarks>
    public bool IsNone => _value == 0;

    /// <summary>Exact equality of the packed value.</summary>
    public bool Equals(ObjectId other) => _value == other._value;

    public override bool Equals(object? obj) => obj is ObjectId other && Equals(other);

    public override int GetHashCode() => _value.GetHashCode();

    public static bool operator ==(ObjectId a, ObjectId b) => a._value == b._value;

    public static bool operator !=(ObjectId a, ObjectId b) => a._value != b._value;

    public override string ToString() => $"ObjectId(Index={Index}, Gen={Generation}, IsNone={IsNone})";
}
