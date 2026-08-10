namespace ProjectEngine;

public static class Time
{
    public static float DeltaTime { get; internal set; }
    public static float UnscaledDeltaTime { get; internal set; }
    public static float FixedDeltaTime { get; set; } = 0.02f;
    public static float TimeScale { get; set; } = 1.0f;
    public static Int128  FrameCount { get; internal set; }

    public static int FrameLoopCount {get; internal set;} = 0;
}
