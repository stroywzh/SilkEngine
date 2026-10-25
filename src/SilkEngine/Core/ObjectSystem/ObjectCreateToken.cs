using SilkEngine.Scripting;

namespace SilkEngine.Core.ObjectSystem;

/// <summary>
/// Construction data handed to the internal <see cref="GameObject"/> constructor only.
/// </summary>
/// <remarks>
/// The token carries the id allocated by <see cref="ObjectManager"/> and the lifetime scope fixed at
/// allocation time . A <see cref="ObjectId.None"/> id means the
/// entity could not be registered and stays uninitialised.
/// </remarks>
internal readonly struct ObjectCreateToken
{
    /// <summary>Id allocated for the new entity.</summary>
    internal readonly ObjectId Id;

    /// <summary>Lifetime scope fixed at allocation time.</summary>
    internal readonly ObjectScope Scope;

    /// <summary>Creates a token.</summary>
    /// <param name="id">Allocated id.</param>
    /// <param name="objectScope">Lifetime scope of the new entity.</param>
    public ObjectCreateToken(ObjectId id, ObjectScope objectScope = ObjectScope.Default)
    {
        Id = id;
        Scope = objectScope;
    }

    public override string ToString() => $"ObjectCreateToken({Id}, {Scope})";
}
