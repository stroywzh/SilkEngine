namespace SilkEngine;

/// <summary>Engine-wide frame timing published by the host main loop.</summary>
/// <remarks>
/// Values are written once per frame on the main-loop thread and are safe to read from other
/// threads. The host main loop announces a frame here, and the render loop snapshots that
/// announcement when it publishes the frame; payloads are never transported as frame data.
/// Timing is unscaled wall-clock time: render, output and VSync waiting are included, and it is
/// never GPU execution time.
/// </remarks>
public static class Time
{
    private static long _deltaSecondsBits;
    private static ulong _frameIndexBits;

    /// <summary>
    /// Gets the duration of the previous completed whole frame in seconds; zero before the first
    /// completed frame. A frame counts as completed even when its render pass failed.
    /// </summary>
    public static double DeltaSeconds =>
        BitConverter.Int64BitsToDouble(Volatile.Read(ref _deltaSecondsBits));

    /// <summary>
    /// Gets the 1-based index of the frame currently being processed; zero before the first frame.
    /// </summary>
    /// <remarks>
    /// The value announced here is snapshotted by the render thread when it publishes the next frame,
    /// so it describes the frame published <em>after</em> this read.
    /// </remarks>
    public static ulong FrameIndex => (ulong)Volatile.Read(ref _frameIndexBits);

    /// <summary>Publishes the values for the frame that is about to be updated.</summary>
    /// <param name="frameIndex">1-based index of the frame being processed (same name as the property).</param>
    /// <param name="deltaSeconds">Duration of the previous completed frame, in seconds.</param>
    internal static void BeginFrame(ulong frameIndex, double deltaSeconds)
    {
        if (!double.IsFinite(deltaSeconds) || deltaSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
        Volatile.Write(ref _frameIndexBits, frameIndex);
        Volatile.Write(ref _deltaSecondsBits, BitConverter.DoubleToInt64Bits(deltaSeconds));
    }

    /// <summary>Clears the facade when a host starts, so no frame is reported as processed yet.</summary>
    internal static void Reset()
    {
        Volatile.Write(ref _deltaSecondsBits, 0L);
        Volatile.Write(ref _frameIndexBits, 0L);
    }
}
