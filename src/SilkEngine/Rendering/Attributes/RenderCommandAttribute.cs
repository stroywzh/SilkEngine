using System;

namespace SilkEngine.Rendering.Attributes;

/// <summary>Declares the command id and executor of a recorded render command.</summary>
/// <remarks>
/// The values are consumed by <c>SilkEngine.SourceGen</c> from the attribute's constructor arguments,
/// so nothing reads them at run time: the attribute stores no state — a metadata-only
/// declaration cannot drift from its generated table.
/// </remarks>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class RenderCommandAttribute : Attribute
{
    /// <summary>Declares the command id and its executor type.</summary>
    /// <param name="cmdType">Command id in 0–255 (the one-byte dispatch key).</param>
    /// <param name="executor">Static class exposing <c>Execute(ReadOnlySpan&lt;byte&gt;, ref CommandContext)</c>.</param>
    public RenderCommandAttribute(int cmdType, Type executor) { }
}
