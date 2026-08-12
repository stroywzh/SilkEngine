namespace SilkEngine;

public abstract class Component : Object
{
    public GameObject GameObject { get; internal set; } = null!;
    // TODO:调用爆null
    public Transform Transform => GameObject.Transform;

    private bool _enabled = true;
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
                return;

            _enabled = value;
            if (_enabled)
                (this as MonoBehaviour)?.OnEnable();
            else
                (this as MonoBehaviour)?.OnDisable();
        }
    }

    public void OnVaildate() { }
}
