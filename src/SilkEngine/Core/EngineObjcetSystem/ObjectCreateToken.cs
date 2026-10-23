using SilkEngine.Runtime;

namespace SilkEngine.Core.ObjcetSystem;

internal readonly struct ComponentCreateToken
{
    internal readonly GameObject GameObject;

    public ComponentCreateToken(GameObject gameObject)
    {
        GameObject = gameObject;
    }
}

internal readonly struct ObjectCreateToken
{
    internal readonly MainThreadContext MainThreadContext;
    internal readonly ObjectId ObjectId;
    internal readonly GameObject? Origin;

    public ObjectCreateToken(ObjectId id, GameObject? origin = null)
    {
        ObjectId = id;
        Origin = origin;
    }
}
