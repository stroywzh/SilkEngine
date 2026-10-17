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
    private static long _unscaledDeltaBits;
    private static ulong _frameCountBits;

    /// <summary>
    /// Gets the duration of the previous successful whole frame in seconds; zero before the first
    /// successful frame.
    /// </summary>
    public static double UnscaledDeltaTime =>
        BitConverter.Int64BitsToDouble(Volatile.Read(ref _unscaledDeltaBits));

    /// <summary>
    /// Gets the 1-based number of the frame currently being processed; zero before the first frame.
    /// </summary>
    /// <remarks>Matches <see cref="Rendering.RenderContext.FrameId"/> of the frame published after
    /// this value is read.</remarks>
    public static ulong FrameCount => (ulong)Volatile.Read(ref _frameCountBits);

    /// <summary>Publishes the values for the frame that is about to be updated.</summary>
    /// <param name="frameCount">1-based id of the frame being processed.</param>
    /// <param name="unscaledDeltaSeconds">Duration of the previous successful frame, in seconds.</param>
    /// <exception cref="ArgumentOutOfRangeException">The delta is negative or not finite.</exception>
    internal static void BeginFrame(ulong frameCount, double unscaledDeltaSeconds)
    {
        if (!double.IsFinite(unscaledDeltaSeconds) || unscaledDeltaSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(unscaledDeltaSeconds));
        Volatile.Write(ref _frameCountBits, frameCount);
        Volatile.Write(
            ref _unscaledDeltaBits,
            BitConverter.DoubleToInt64Bits(unscaledDeltaSeconds)
        );
    }

    /// <summary>Clears the facade when a host starts, so no frame is reported as processed yet.</summary>
    internal static void Reset()
    {
        Volatile.Write(ref _unscaledDeltaBits, 0L);
        Volatile.Write(ref _frameCountBits, 0L);
    }
}
