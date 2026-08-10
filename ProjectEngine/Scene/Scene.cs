using System.Collections.Generic;

namespace ProjectEngine;

public class Scene
{
    public string Name { get; }
    internal List<GameObject> _rootObjects = new();
    public Scene(string name) => Name = name;
    public void AddRootObject(GameObject go) => _rootObjects.Add(go);
    public GameObject[] GetRootGameObjects() => _rootObjects.ToArray();
}
