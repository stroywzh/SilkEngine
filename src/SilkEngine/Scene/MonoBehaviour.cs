namespace SilkEngine;

public abstract class MonoBehaviour : Component
{
    public virtual void OnAwake() { }

    public virtual void OnStart() { }

    public virtual void OnEnable() { }

    public virtual void OnDisable() { }

    public virtual void OnUpdate(float deltaTime) { }

    public virtual void OnFixedUpdate(float deltaTime) { }

    public virtual void OnLateUpdate() { }

    public virtual void OnPostRender() { }

    public virtual void OnDestroy() { }
}
