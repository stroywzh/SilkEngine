using System.Collections.ObjectModel;
using SilkEngine.Scripting;

namespace SilkEngine.Core.ObjectSystem;

/// <summary>
/// Container of all game objects that share one <see cref="ObjectScope"/>.
/// </summary>
internal sealed class ObjectContainer
{
    private readonly List<GameObject> _gameObjects = new();
    private ReadOnlyCollection<GameObject> _snapShot = Array.AsReadOnly(Array.Empty<GameObject>());
    private bool _dirty = true;

    /// <summary>
    /// Read-only snapshot of the contained game objects, rebuilt after every change.
    /// </summary>
    /// <remarks>
    /// A real <see cref="ReadOnlyCollection{T}"/> is returned, not an array typed as an interface: a
    /// caller cannot downcast it and mutate engine state through the snapshot.
    /// </remarks>
    public IReadOnlyList<GameObject> GameObjects
    {
        get
        {
            if (_dirty)
            {
                _snapShot = Array.AsReadOnly(_gameObjects.ToArray());
                _dirty = false;
            }
            return _snapShot;
        }
    }

    /// <summary>Number of contained game objects.</summary>
    public int Count => _gameObjects.Count;

    public bool Contains(GameObject go)
    {
        return _gameObjects.Contains(go);
    }

    /// <summary>Adds an entity to this container unless it is already present.</summary>
    /// <returns><c>true</c> when the entity was added; <c>false</c> when it is null or already present.</returns>
    /// <remarks>
    /// The caller <see cref="ObjectManager"/> owns the diagnostics: the container only reports the outcome, so
    /// the duplicate warning exists in exactly one place.
    /// </remarks>
    public bool Add(GameObject go)
    {
        if (go is null)
            return false;

        if (_gameObjects.Contains(go))
            return false;

        _gameObjects.Add(go);
        _dirty = true;
        return true;
    }

    /// <summary>Remove an entity in this container.</summary>
    /// <returns><c>true</c> when the entity was removed.</returns>
    public bool Remove(GameObject go)
    {
        var removed = _gameObjects.Remove(go);
        if (removed)
        {
            _dirty = true;
        }

        return removed;
    }

    /// <summary>
    /// Clear all entity in this container.
    /// </summary>
    public void ClearAll()
    {
        _gameObjects.Clear();
        _dirty = true;
    }
}
