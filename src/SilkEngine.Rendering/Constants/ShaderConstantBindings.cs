namespace SilkEngine.Rendering;

/// <summary>Constant-binding points used by the built-in shaders.</summary>
public static class ShaderConstantBindings
{
    /// <summary>Per-frame constants: time and viewport.</summary>
    public const ushort Frame = 0;

    /// <summary>Per-material constants.</summary>
    public const ushort Material = 1;

    /// <summary>Per-draw constants.</summary>
    public const ushort Draw = 2;
}
