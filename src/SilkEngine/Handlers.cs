// This file stores the generic resource handle shared by the asset layer.
// Typed rendering handles live in SilkEngine.Rendering.Abstraction/Handles.cs.
namespace SilkEngine;

/// <summary>
/// Generic resource-management wrapper around an opaque identifier.
/// </summary>
/// <typeparam name="T">Underlying identifier type.</typeparam>
/// <remarks>
/// Kept public as the resource-management wrapper (C-02): <see cref="Core.AssetSystem.AssetHandle"/>
/// stores a <c>Handle&lt;ulong&gt;</c>. Typed rendering handles are separate
/// <c>readonly record struct</c> types and do not use this wrapper (D-A13 defers
/// <c>Handle&lt;TTag&gt;</c>).
/// </remarks>
public readonly struct Handle<T>
    where T : struct
{
    /// <summary>The wrapped identifier.</summary>
    public readonly T Value;

    /// <summary>Wraps <paramref name="value"/>.</summary>
    public Handle(T value)
    {
        Value = value;
    }
}
