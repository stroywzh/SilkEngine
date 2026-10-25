namespace SilkEngine.Core.ObjcetSystem;

/// <summary>
/// Container of all the EngineGameObjects
/// </summary>
public class ObjectContainer
{
    private readonly List<GameObject> _gameObjects = new();
    private GameObject[] _snapShot = Array.Empty<GameObject>();
    private bool _dirty = true;

    /// <summary>
    /// All GameObjects in managed.
    /// </summary>
    public GameObject[] GameObjects
    {
        get
        {
            if (_dirty)
            {
                _snapShot = _gameObjects.ToArray();
                _dirty = false;
            }
            return _snapShot;
        }
    }
    public int Count => _gameObjects.Count;

    public bool Contains(GameObject go)
    {
        return _gameObjects.Contains(go);
    }

    public void Add(GameObject go)
    {
        if (go is null)
        {
            Log.Warning("You're trying to add a null GameObject to the Container.");
            return;
        }

        if (_gameObjects.Contains(go))
        {
            Log.Warning("You're trying to add same GameObject to one Container.");
        }

        _gameObjects.Add(go);
        _dirty = true;
    }

    public bool Remove(GameObject go)
    {
        var removed = _gameObjects.Remove(go);
        if (removed)
        {
            _dirty = true;
        }

        return removed;
    }

    public void ClearAll()
    {
        _gameObjects.Clear();
        _dirty = true;
    }
}
