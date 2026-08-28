using System.Collections.Generic;

namespace SilkEngine.Scene;

/// <summary>场景容器：根对象列表（子树经 Transform.Children 嵌套）；由 SceneManager 加载与切换。</summary>
public class Scene
{
    /// <summary>场景名称（.scene JSON 的 Name 键）。</summary>
    public string Name { get; }
    internal List<GameObject> _rootObjects = new();

    /// <summary>场景上下文（SceneManager.Create 装配；null 表示未绑定管理器的独立场景）。</summary>
    internal SceneContext? Context { get; set; }

    /// <summary>创建命名场景（初始无根对象）。</summary>
    /// <param name="name">场景名称</param>
    public Scene(string name) => Name = name;

    /// <summary>
    /// 业务统一创建入口：创建并绑定本场景上下文的对象，登记为根对象并立即注册组件。
    /// 组件经上下文注册表登记（不依赖 Services 回退链）。
    /// </summary>
    /// <param name="name">对象名称</param>
    /// <returns>已绑定上下文并登记进本场景的对象</returns>
    public GameObject CreateGameObject(string name = "GameObject")
    {
        var go = new GameObject(name, Context);
        _rootObjects.Add(go);
        Context?.Manager.RegisterSceneObject(go);
        return go;
    }

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

    /// <summary>对象是否属于本场景（根对象或任一根对象的子树）。</summary>
    /// <param name="go">要查询的对象</param>
    /// <returns>属于本场景为 true</returns>
    public bool Contains(GameObject go)
    {
        foreach (var root in _rootObjects)
            if (ContainsInHierarchy(root, go))
                return true;
        return false;
    }

    private static bool ContainsInHierarchy(GameObject node, GameObject target)
    {
        if (ReferenceEquals(node, target))
            return true;
        foreach (var child in node.Transform.Children)
            if (child.GameObject is { } cgo && ContainsInHierarchy(cgo, target))
                return true;
        return false;
    }
}
