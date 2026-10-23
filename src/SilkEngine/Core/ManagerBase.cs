using SilkEngine.Runtime;

namespace SilkEngine.Core;

internal abstract class ManagerBase<TManager> : SingletonInstance<TManager>, IUpdateable
    where TManager : class
{
    protected ManagerBase()
        : base()
    {
        UpdateDriver.Register(this);
    }

    public virtual void FixedUpdateInternal() { }

    public virtual void LateUpdateInternal() { }

    public virtual void UpdateInternal() { }
}
