using System;

namespace SilkEngine.Rendering.Attributes;

[AttributeUsage(AttributeTargets.Struct)]
public sealed class RenderCommandAttribute : Attribute
{
    public int CommandType { get; }
    public Type Executor { get; }

    public RenderCommandAttribute(int cmdType, Type executor)
    {
        CommandType = cmdType;
        Executor = executor;
    }
}
