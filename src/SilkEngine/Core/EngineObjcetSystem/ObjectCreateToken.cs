using SilkEngine.Runtime;
using SilkEngine.Runtime.Host;

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
    internal readonly ObjectId ObjectId;
    internal readonly GameObject? Origin;

    public ObjectCreateToken(ObjectId id, GameObject? origin = null)
    {
        ObjectId = id;
        Origin = origin;
    }

    public override string ToString()
    {
        return $"ObjectCreateToken({ObjectId}, Origin={Origin})";
    }
}
