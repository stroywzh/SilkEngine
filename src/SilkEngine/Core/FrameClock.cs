namespace SilkEngine.Core;

/// <summary>
/// 帧时钟（原 EngineLoop.GetDeltaTime + Time 门面同步职责）：以墙钟时间差值计算 dt（0.1s 钳制），
/// 并同步 Time.UnscaledDeltaTime/DeltaTime/FrameCount（Tick 之前帧首执行）。
/// </summary>
internal sealed class FrameClock
{
    private readonly Func<DateTime> _utcNow;
    private DateTime _lastTime;

    /// <summary>以真实墙钟时间驱动（EngineLoop 默认）。</summary>
    public FrameClock()
        : this(() => DateTime.UtcNow)
    {
    }

    /// <summary>测试注入：以自定义时间源驱动（dt 钳制与 Time 同步语义可测）。</summary>
    /// <param name="utcNow">UTC 时间源</param>
    internal FrameClock(Func<DateTime> utcNow)
    {
        _utcNow = utcNow;
        _lastTime = utcNow();
    }

    /// <summary>
    /// 帧首推进：计算并钳制本帧增量时间，同步 Time 门面（DeltaTime 乘 TimeScale），
    /// 递增 FrameCount；调用方应仅每帧一次（返回值即本帧 dt）。
    /// </summary>
    /// <returns>本帧增量时间（秒，≤ 0.1）</returns>
    public float Tick()
    {
        var now = _utcNow();
        float dt = (float)(now - _lastTime).TotalSeconds;
        _lastTime = now;
        dt = System.Math.Min(dt, 0.1f);
        Time.UnscaledDeltaTime = dt;
        Time.DeltaTime = dt * Time.TimeScale;
        Time.FrameCount++;
        return dt;
    }

    /// <summary>重置帧间基准时间（暂停恢复与 Initialize 首帧基准；原 EngineLoop._lastTime 复位语义）。</summary>
    public void Reset() => _lastTime = _utcNow();
}
