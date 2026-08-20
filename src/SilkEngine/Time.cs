namespace SilkEngine.Core;

/// <summary>
/// 全局时间门面（主线程帧序）：DeltaTime/UnscaledDeltaTime/FrameCount 由 EngineLoop
/// 每帧帧首更新（Tick 之前），FixedDeltaTime 与 EngineLoop.FixedDeltaTime 双向同步。
/// </summary>
public static class Time
{
    /// <summary>上一帧耗时（秒）乘以 TimeScale 后的值；帧首由 EngineLoop 更新，供帧内逻辑使用。</summary>
    public static float DeltaTime { get; internal set; } = 0.0f;

    /// <summary>不受 TimeScale 影响的原始帧耗时（秒）；帧首由 EngineLoop 更新。</summary>
    public static float UnscaledDeltaTime { get; internal set; }

    /// <summary>固定步长（秒），默认 0.02；FixedTick 每次以该步长推进。</summary>
    public static float FixedDeltaTime { get; internal set; } = 0.02f;

    /// <summary>时间缩放系数，1.0 为正常速度；影响 DeltaTime（UnscaledDeltaTime 不受影响）。</summary>
    public static float TimeScale { get; set; } = 1.0f;

    /// <summary>已渲染帧计数；帧首由 EngineLoop 递增。</summary>
    public static long FrameCount { get; internal set; } = 0;
}
