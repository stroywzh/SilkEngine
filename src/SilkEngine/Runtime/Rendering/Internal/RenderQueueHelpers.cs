namespace SilkEngine.Runtime.Rendering.Internal;

internal static class RenderQueueConstHelper
{
    public const int Min = 0;

    // 16bit, 0xFFFF, 65535
    public const int QueueMax = ushort.MaxValue;

    // 32bit depth, 4,294,967,295
    public const uint DepthMax = uint.MaxValue;

    // 32bit materials, 4,294,967,295
    public const uint MaterialMax = uint.MaxValue;

    public const int TransparentStart = (int)RenderQueueGroup.Transparent;
}


