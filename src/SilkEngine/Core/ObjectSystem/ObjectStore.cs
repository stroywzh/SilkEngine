using System.Collections.Generic;
using SilkEngine.Scripting;

namespace SilkEngine.Core.ObjectSystem;

/// <summary>
/// Owns the object-id space and the id to object map: the single place that keeps slots, generations
/// and the lookup table consistent.
/// </summary>
internal sealed class ObjectStore
{
    private readonly Dictionary<ObjectId, GameObject> _objectsById = new();
    private readonly List<uint> _generations = new() { 0 };
    private readonly Stack<uint> _freeIndices = new();
    private uint _nextIndex = 1;

    /// <summary>
    /// Allocates an <see cref="ObjectId"/>. The caller must <see cref="ReleaseId"/> it when
    /// construction fails.
    /// </summary>
    internal ObjectId AllocateId()
    {
        uint index;
        uint generation;

        if (_freeIndices.Count > 0)
        {
            // Reuse: bump the generation so old handles become invalid.
            index = _freeIndices.Pop();
            generation = ++_generations[(int)index];
        }
        else
        {
            // Fresh slot: index increments, generation starts at 0.
            index = _nextIndex++;
            generation = 0;
            _generations.Add(0); // Reserve a slot for the new index.
        }

        return ObjectId.Create(index, generation);
    }

    /// <summary>
    /// Rolls an allocation back (when construction failed). Only the slot is returned; the generation
    /// is not rewound, so the next allocation invalidates the old id.
    /// </summary>
    internal void ReleaseId(ObjectId id)
    {
        if (id.IsNone)
            return;

        _freeIndices.Push(id.Index);
    }

    /// <summary>Looks an entity up by id.</summary>
    internal bool TryGet(ObjectId id, out GameObject go) => _objectsById.TryGetValue(id, out go!);

    /// <summary>Registers an entity under its id; a duplicate id is rejected.</summary>
    /// <returns><c>true</c> when the id was free and is now registered.</returns>
    internal bool Add(GameObject go) => _objectsById.TryAdd(go.Id, go);

    /// <summary>Removes the entity registered under <paramref name="id"/>.</summary>
    /// <returns><c>true</c> when an entity was registered and is now removed.</returns>
    internal bool Remove(ObjectId id) => _objectsById.Remove(id);
}
