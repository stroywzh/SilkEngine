using System.Collections.Generic;
using System.Linq;

namespace SilkEngine;

public sealed class ComponentGroup
{
    public System.Type ComponentType { get; init; } = null!;
    public List<Component> Components { get; init; } = [];
}

public sealed class FrameSnapshot
{
    public long FrameCount { get; internal set; }
    public Scene? ActiveScene { get; internal set; }
    internal List<ComponentGroup> Groups { get; } = [];

    public IReadOnlyList<T> GetComponents<T>() where T : Component
    {
        var group = Groups.Find(g => g.ComponentType == typeof(T));
        return group == null
            ? System.Array.Empty<T>()
            : group.Components.Cast<T>().ToList().AsReadOnly();
    }
}
