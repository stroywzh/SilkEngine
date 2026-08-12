namespace SilkEngine;

public abstract class Component : Object
{
    public GameObject GameObject { get; internal set; } = null!;
    public Transform Transform => GameObject.Transform;

    internal bool Awaked;   // OnAwake 已触发
    internal bool Started;  // OnStart 已触发

    private bool _enabled = true;
    private bool _enableFired;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
                return;
            _enabled = value;
            RecomputeActiveState();
        }
    }

    internal void RecomputeActiveState()
    {
        bool shouldBeActive = _enabled && GameObject.IsActiveInHierarchy;
        if (shouldBeActive && !_enableFired)
        {
            _enableFired = true;
            OnEnable();
        }
        else if (!shouldBeActive && _enableFired)
        {
            _enableFired = false;
            OnDisable();
        }
    }

    public void OnVaildate() { }

    public virtual void OnEnable() { }

    public virtual void OnDisable() { }

    public virtual void OnDestroy() { }
}
