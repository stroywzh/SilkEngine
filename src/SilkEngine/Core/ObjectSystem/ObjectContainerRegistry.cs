using System.Collections.Generic;

namespace SilkEngine.Core.ObjectSystem;

/// <summary>Maps an <see cref="ObjectScope"/> to its container; the only owner of that map.</summary>
internal sealed class ObjectContainerRegistry
{
    private readonly Dictionary<ObjectScope, ObjectContainer> _containers = new();

    /// <summary>Registers (or replaces) the container of a scope.Noramlly one scope with one container and contianer will not change during runtime.</summary>
    internal void Register(ObjectScope objectScope, ObjectContainer container) =>
        _containers[objectScope] = container;

    /// <summary>Looks the container of a scope up.</summary>
    internal bool TryGet(ObjectScope objectScope, out ObjectContainer container) =>
        _containers.TryGetValue(objectScope, out container!);

    /// <summary>Gets all registered containers (one per scope).</summary>
    internal IEnumerable<ObjectContainer> Values => _containers.Values;
}
