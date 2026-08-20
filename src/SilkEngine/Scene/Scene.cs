using System.Collections.Generic;

namespace SilkEngine.Scene;

public class Scene
{
    public string Name { get; }
    internal List<GameObject> _rootObjects = new();

    public Scene(string name) => Name = name;

    /// <summary>添加根对象；同实例重复添加抛 InvalidOperationException。</summary>
    public void AddRootObject(GameObject go)
    {
        if (_rootObjects.Contains(go))
            throw new InvalidOperationException(
                $"GameObject '{go.Name}' is already a root object of scene '{Name}'"
            );
        _rootObjects.Add(go);
    }

    public GameObject[] GetRootGameObjects() => _rootObjects.ToArray();
}
