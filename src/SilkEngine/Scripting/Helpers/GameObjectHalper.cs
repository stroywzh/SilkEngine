using SilkEngine.Core;
using SilkEngine.Core.ObjcetSystem;

namespace SilkEngine;

/// <summary>
/// 对外统一门面。底层仍是 ManagerBase&lt;ObjectManager&gt; 单例，
/// </summary>
public static class GameObjectHelper
{
    public static GameObject Instantiate(GameObject? prefab = null) =>
        ObjectManager.Instance.CreateFromPrefab(prefab);

    public static void Destroy(GameObject go) => ObjectManager.Instance.Destroy(go);

    public static void DestroyImmediate(GameObject go) => ObjectManager.Instance.Destroy(go);

    internal static bool TryGet(ObjectId id, out GameObject go) =>
        ObjectManager.Instance.TryGetById(id, out go);
}
