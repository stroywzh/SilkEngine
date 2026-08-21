using SilkEngine.Core;

namespace SilkEngine.Tests.Core;

/// <summary>
/// FrameScheduler（原 EngineLoop.TickFrame 语义）：固定步长累加 + 余量结转 + FixedDeltaTime 委托
/// （与 Time.FixedDeltaTime 双向同步，非法值抛错沿用 FixedStepAccumulator setter）。
/// </summary>
[Collection("FrameClock")]
public class FrameSchedulerTests
{
    [Fact]
    public void Tick_AccumulatesFixedSteps_FixedBeforeTickBeforeLate()
    {
        var s = new FrameScheduler();
        var log = new List<(string Kind, float Arg)>();
        s.Tick(
            0.05f,
            f => log.Add(("fixed", f)),
            d => log.Add(("tick", d)),
            () => log.Add(("late", 0f))
        );

        Assert.Equal(new[] { "fixed", "fixed", "tick", "late" }, log.Select(e => e.Kind));
        Assert.All(log.Where(e => e.Kind == "fixed"), e => Assert.Equal(0.02f, e.Arg));
        Assert.Equal(0.05f, Assert.Single(log, e => e.Kind == "tick").Arg);
    }

    [Fact]
    public void Tick_TwoFramesOfSixteenMs_CarriesRemainder()
    {
        var s = new FrameScheduler();
        var log = new List<string>();
        s.Tick(0.016f, _ => log.Add("fixed"), _ => log.Add("tick"), () => log.Add("late"));
        Assert.Equal(new[] { "tick", "late" }, log); // 0.016 < 0.02 → 无 FixedTick

        log.Clear();
        s.Tick(0.016f, _ => log.Add("fixed"), _ => log.Add("tick"), () => log.Add("late"));
        Assert.Equal(new[] { "fixed", "tick", "late" }, log); // 0.032 跨过 0.02 → 一次，余 0.012

        log.Clear();
        s.Tick(0.016f, _ => log.Add("fixed"), _ => log.Add("tick"), () => log.Add("late"));
        Assert.Equal(new[] { "fixed", "tick", "late" }, log); // 余 0.012 + 0.016 = 0.028 → 一次，余 0.008
    }

    [Fact]
    public void FixedDeltaTime_Set_DelegatesToAccumulator_AndSyncsTime()
    {
        var s = new FrameScheduler();
        Assert.Equal(0.02f, s.FixedDeltaTime);
        Assert.Equal(0.02f, Time.FixedDeltaTime); // ctor 初值同步（原 EngineLoop ctor 语义）
        s.FixedDeltaTime = 0.05f;
        Assert.Equal(0.05f, s.FixedDeltaTime);
        Assert.Equal(0.05f, Time.FixedDeltaTime);

        float fdt = 0f;
        int steps = 0;
        s.Tick(0.11f, f => { fdt = f; steps++; }, _ => { }, () => { });
        Assert.Equal(2, steps); // 0.11 = 2×0.05 + 余 0.01
        Assert.Equal(0.05f, fdt); // 固定步长值传入 FixedTick
    }

    [Fact]
    public void FixedDeltaTime_InvalidValue_Throws_WithoutSyncingTime()
    {
        var s = new FrameScheduler();
        float before = Time.FixedDeltaTime;
        Assert.Throws<ArgumentOutOfRangeException>(() => s.FixedDeltaTime = 0f);
        Assert.Throws<ArgumentOutOfRangeException>(() => s.FixedDeltaTime = -0.02f);
        Assert.Throws<ArgumentOutOfRangeException>(() => s.FixedDeltaTime = float.NaN);
        Assert.Equal(before, Time.FixedDeltaTime); // setter 抛错后 Time 未同步
    }
}
