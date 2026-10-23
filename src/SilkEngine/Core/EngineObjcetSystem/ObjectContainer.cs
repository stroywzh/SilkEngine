namespace SilkEngine.Core.ObjcetSystem;

public class ObjectContainer
{
    private List<GameObject> _gameObjects = new();

    public GameObject[] ActiveGameObjects => _gameObjects.ToArray();

    public bool Contains(GameObject go)
    {
        return _gameObjects.Contains(go);
    }

    public void Add(GameObject go) { }

    public void Remove(GameObject go) { }
}
