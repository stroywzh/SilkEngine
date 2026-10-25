using System.Runtime.CompilerServices;

namespace SilkEngine.Rendering;

/// <summary>Queue buckets packed into the render-item sort key.</summary>
/// <remarks>
/// Values live in the 16-bit queue space and are ordered: a larger value draws later. They are part of
/// the packed sort key, so renumbering a member reorders existing recordings.
/// </remarks>
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

/// <summary>
/// Single source of the transparent-queue threshold. Internal because the CPU sorting queue that
/// consumes it is not public yet; the predicate must not be duplicated per consumer.
/// </summary>
internal static class RenderQueueGroupExtensions
{
    /// <summary>Whether <paramref name="queue"/> belongs to the transparent range.</summary>
    /// <param name="queue">Queue value in the 16-bit queue space.</param>
    internal static bool IsTransparent(this ushort queue) =>
        queue >= (ushort)RenderQueueGroup.Transparent;
}
