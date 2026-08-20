using System.Collections.Generic;

namespace SilkEngine.Scene;

/// <summary>场景容器：根对象列表（子树经 Transform.Children 嵌套）；由 SceneManager 加载与切换。</summary>
public class Scene
{
    /// <summary>场景名称（.scene JSON 的 Name 键）。</summary>
    public string Name { get; }
    internal List<GameObject> _rootObjects = new();

    /// <summary>创建命名场景（初始无根对象）。</summary>
    /// <param name="name">场景名称</param>
    public Scene(string name) => Name = name;

    /// <summary>
    /// 添加根对象（无父级的对象；子树对象不单独登记）。
    /// </summary>
    /// <param name="go">要添加的对象</param>
    /// <exception cref="InvalidOperationException">同实例重复添加</exception>
    public void AddRootObject(GameObject go)
    {
        if (_rootObjects.Contains(go))
            throw new InvalidOperationException(
                $"GameObject '{go.Name}' is already a root object of scene '{Name}'"
            );
        _rootObjects.Add(go);
    }

    /// <summary>返回根对象数组（快照副本，不含子树对象）。</summary>
    public GameObject[] GetRootGameObjects() => _rootObjects.ToArray();
}
