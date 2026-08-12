using System;
using System.Collections.Generic;
using System.Linq;

namespace SilkEngine;

public sealed class ComponentRegistry
{
    private readonly Dictionary<Type, List<Component>> _typeMap = new();
    private readonly List<Component> _pendingAdds = [];

    public void Register(Component c)
    {
        if (_pendingAdds.Contains(c))
            return;
        var t = c.GetType();
        if (_typeMap.TryGetValue(t, out var list) && list.Contains(c))
            return;
        _pendingAdds.Add(c);
    }

    public void Unregister(Component c)
    {
        _pendingAdds.Remove(c);
        var t = c.GetType();
        if (_typeMap.TryGetValue(t, out var list))
            list.Remove(c);
    }

    public void ApplyPending()
    {
        foreach (var c in _pendingAdds)
        {
            var t = c.GetType();
            if (!_typeMap.ContainsKey(t))
                _typeMap[t] = [];
            _typeMap[t].Add(c);
        }
        _pendingAdds.Clear();
    }

    public void RefreshSnapshot(FrameSnapshot snapshot)
    {
        snapshot.Groups.Clear();
        foreach (var kvp in _typeMap)
        {
            snapshot.Groups.Add(new ComponentGroup
            {
                ComponentType = kvp.Key,
                Components = [.. kvp.Value]
            });
        }
    }

    public IReadOnlyList<T> GetOfType<T>() where T : Component
    {
        var result = new List<T>();
        foreach (var kvp in _typeMap)
        {
            if (kvp.Key == typeof(T) || kvp.Key.IsSubclassOf(typeof(T)))
                foreach (var c in kvp.Value)
                    result.Add((T)c);
        }
        return result.AsReadOnly();
    }
}
