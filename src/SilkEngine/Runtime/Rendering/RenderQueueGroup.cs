using System.Runtime.CompilerServices;

namespace SilkEngine.Runtime.Rendering;

public enum RenderQueueGroup : ushort
{
    Background = 1000,
    Geometry = 2000,
    ZTest = 2450,
    Transparent = 3000,
    Overlay = 4000,
}

public static class RenderQueueGroupHelper
{
    public static bool IsTransparent(this int queue) =>
        queue >= (ushort)RenderQueueGroup.Transparent;

    public static bool IsTransparent(this ushort queue) =>
        queue >= (ushort)RenderQueueGroup.Transparent;

    public static bool IsTransparent(this RenderQueueGroup group) =>
        group >= RenderQueueGroup.Transparent;
}
