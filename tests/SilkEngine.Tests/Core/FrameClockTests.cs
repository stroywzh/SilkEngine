using SilkEngine.Core;

namespace SilkEngine.Tests.Core;

/// <summary>
/// FrameClock（原 EngineLoop.GetDeltaTime + Time 门面同步语义）：dt 计算 + 0.1s 钳制 +
/// UnscaledDeltaTime/DeltaTime/FrameCount 同步；经注入时间源测试（全局 Time 门面状态测试后还原）。
/// </summary>
[Collection("FrameClock")]
public class FrameClockTests : IDisposable
{
    private readonly float _originalDt = Time.DeltaTime;
    private readonly float _originalUnscaled = Time.UnscaledDeltaTime;
    private readonly float _originalScale = Time.TimeScale;
    private readonly long _originalFrame = Time.FrameCount;

    /// <summary>测试级清理：还原 Time 门面（并行集合隔离：其他测试不读 FrameClock 写字段，仍按惯例还原）</summary>
    public void Dispose()
    {
        Time.DeltaTime = _originalDt;
        Time.UnscaledDeltaTime = _originalUnscaled;
        Time.TimeScale = _originalScale;
        Time.FrameCount = _originalFrame;
    }

    private sealed class TimeSource
    {
        public DateTime Value = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public DateTime Now() => Value;
    }

    [Fact]
    public void Tick_ClampsDeltaToOneTenthSecond()
    {
        var src = new TimeSource();
        var clock = new FrameClock(src.Now);
        src.Value = src.Value.AddSeconds(5);
        Assert.Equal(0.1f, clock.Tick());
        Assert.Equal(0.1f, Time.UnscaledDeltaTime);
    }

    [Fact]
    public void Tick_ShortDelta_NoClamp()
    {
        var src = new TimeSource();
        var clock = new FrameClock(src.Now);
        src.Value = src.Value.AddMilliseconds(16);
        Assert.Equal(0.016f, clock.Tick(), 3);
    }

    [Fact]
    public void Tick_SyncsTimeFacade_WithTimeScale()
    {
        var src = new TimeSource();
        var clock = new FrameClock(src.Now);
        Time.TimeScale = 2.0f;
        src.Value = src.Value.AddMilliseconds(16);
        float dt = clock.Tick();
        Assert.Equal(0.016f, dt, 3);
        Assert.Equal(dt, Time.UnscaledDeltaTime);
        Assert.Equal(dt * 2.0f, Time.DeltaTime, 3);
        Assert.Equal(_originalFrame + 1, Time.FrameCount);
    }

    [Fact]
    public void Reset_ZeroesDeltaBaseline()
    {
        var src = new TimeSource();
        var clock = new FrameClock(src.Now);
        src.Value = src.Value.AddSeconds(5);
        clock.Reset();
        src.Value = src.Value.AddMilliseconds(4);
        Assert.Equal(0.004f, clock.Tick(), 3);
    }
}
