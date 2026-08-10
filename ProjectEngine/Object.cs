using System;
using System.Linq;

namespace ProjectEngine;

public abstract class Object
{
    private static int _nextID = 0;
    private readonly int _id = Interlocked.Increment(ref _nextID);
    public string Name { get; set; } = "";
    public int GetInstanceID() => _id;
    public static event Action<Object, float>? DestroyHandler;
    public static void Destroy(Object obj, float delay = 0f)
    {
        if (obj is GameObject go)
            DestroyRecursive(go);
        DestroyHandler?.Invoke(obj, delay);
    }

    private static void DestroyRecursive(GameObject go)
    {
        foreach (var child in go.Transform.Children.ToArray())
            DestroyRecursive(child.GameObject!);
        go.IsActive = false;
        foreach (var c in go._components)
            c.Enabled = false;
    }

    public static Object Instantiate(Object original)
    {
        if (original is GameObject go)
        {
            var clone = new GameObject(go.Name + "(Clone)") { IsActive = go.IsActive };
            clone.Transform.LocalPosition = go.Transform.LocalPosition;
            clone.Transform.LocalRotation = go.Transform.LocalRotation;
            clone.Transform.LocalScale = go.Transform.LocalScale;
            foreach (var child in go.Transform.Children)
                if (Instantiate(child.GameObject) is GameObject cc)
                    cc.Transform.SetParent(clone.Transform);
            return clone;
        }
        throw new NotSupportedException($"Instantiate not supported for {original.GetType()}");
    }
}
