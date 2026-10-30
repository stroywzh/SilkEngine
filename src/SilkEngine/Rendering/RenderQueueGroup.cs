using System.Runtime.CompilerServices;

namespace SilkEngine.Rendering;

/// <summary>Queue buckets packed into the render-item sort key.</summary>
public enum RenderQueueGroup : ushort
{
    /// <summary>Background geometry (1000): drawn before everything else.</summary>
    Background = 1000,

    /// <summary>Opaque geometry (2000).</summary>
    Geometry = 2000,

    /// <summary>Depth-tested geometry that sorts before transparency (2450).</summary>
    ZTest = 2450,

    /// <summary>Transparent geometry (3000); also the threshold used by the transparency predicate.</summary>
    Transparent = 3000,

    /// <summary>Overlay/UI geometry (4000): drawn last.</summary>
    Overlay = 4000,
}

/// <summary>Single source of the transparent-queue threshold.</summary>
internal static class RenderQueueGroupExtensions
{
    /// <summary>Whether <paramref name="queue"/> belongs to the transparent range.</summary>
    internal static bool IsTransparent(this ushort queue) =>
        queue >= (ushort)RenderQueueGroup.Transparent;
}
