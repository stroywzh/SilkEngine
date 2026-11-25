namespace SilkEngine.Rendering.Abstraction;

/// <summary>Render-side identity of one bound asset: a slot plus the generation that owns it.</summary>
/// <remarks>Handles are opaque: pass one back to the resource table that issued it. A handle stops being valid
/// when its binding is released, and a stale handle never resolves to a newer binding that reuses its slot.</remarks>
public readonly record struct RenderHandle(uint Slot, uint Generation)
{
    /// <summary>True when this value can refer to a live binding; false for the default value.</summary>
    public bool IsValid => Generation != 0;
}