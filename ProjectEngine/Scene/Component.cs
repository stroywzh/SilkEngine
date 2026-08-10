namespace ProjectEngine;

public abstract class Component : Object
{
    public GameObject GameObject { get; internal set; } = null!;
    public Transform Transform => GameObject.Transform;
}
