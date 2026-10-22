namespace SilkEngine.Core.EngineObjcetSystem;

public class ObjectContainer
{
    private static List<GameObject> _gameObjects = new();

    public GameObject[] ActiveGameObjects => _gameObjects.ToArray();

    public void Register(GameObject go) { }

    public void UnRegister(GameObject go) { }
}
