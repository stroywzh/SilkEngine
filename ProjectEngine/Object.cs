using System;

namespace ProjectEngine;

public abstract class Object
{
    private static int _nextID = 1;
    private readonly int _id = _nextID++;
    public string Name { get; set; } = "";
    public int GetInstanceID() => _id;
    public static event Action<Object, float>? DestroyHandler;
    public static void Destroy(Object obj, float delay = 0f) => DestroyHandler?.Invoke(obj, delay);
}
