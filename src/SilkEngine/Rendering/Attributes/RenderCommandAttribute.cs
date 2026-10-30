using System;

namespace SilkEngine.Rendering.Attributes;

/// <summary>Declares the command id and executor of a recorded render command.</summary>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class RenderCommandAttribute : Attribute
{
    /// <summary>Declares the command id and its executor type.</summary>
    public RenderCommandAttribute(int cmdType, Type executor) { }
}
