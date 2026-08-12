namespace SilkEngine;

public abstract class MonoBehaviour : Component
{
    public virtual void OnAwake() { }
    public virtual void OnStart() { }
    public virtual void OnEnable() { }
    public virtual void OnDisable() { }
    public virtual void OnTick(float deltaTime) { }
    public virtual void OnFixedTick(float deltaTime) { }
    public virtual void OnLateTick() { }
    public virtual void OnPostRender() { }
    public virtual void OnDestroy() { }
}
