namespace SilkEngine.Core.ObjectSystem;

/// <summary>Lifetime scope of a game object, selected by the container it lives in.</summary>
internal enum ObjectScope : byte
{
    Temperary = 0,
    /// <summary>Default scope: torn down with the running engine.</summary>
    Default = 1,
    /// <summary>Outlives a normal teardown (survives scene/engine reset).</summary>
    Persistent = 2,
}
