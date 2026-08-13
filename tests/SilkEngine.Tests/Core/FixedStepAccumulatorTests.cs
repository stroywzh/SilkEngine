using SilkEngine.Core;

namespace SilkEngine.Tests.Core;

public class FixedStepAccumulatorTests
{
    [Fact]
    public void Advance_SmallDt_NoFixedStep_Accumulates()
    {
        var acc = new FixedStepAccumulator { FixedDeltaTime = 0.02f };
        Assert.Equal(0, acc.Advance(0.016f));
        Assert.Equal(0.016f, acc.Remainder, 5);
    }

    [Fact]
    public void Advance_TwoSmallDts_TriggersOnce_KeepsResidual()
    {
        var acc = new FixedStepAccumulator { FixedDeltaTime = 0.02f };
        Assert.Equal(0, acc.Advance(0.016f));
        Assert.Equal(1, acc.Advance(0.016f));       // 0.032 ≥ 0.02 → 1 步
        Assert.Equal(0.012f, acc.Remainder, 5);     // 剩余累积 0.012
    }

    [Fact]
    public void Advance_LargeDt_TriggersMultiple()
    {
        var acc = new FixedStepAccumulator { FixedDeltaTime = 0.02f };
        Assert.Equal(2, acc.Advance(0.05f));        // 0.05 = 2×0.02 + 余 0.01
        Assert.Equal(0.01f, acc.Remainder, 5);
    }

    [Fact]
    public void Advance_ExactMultiple_NoResidual()
    {
        var acc = new FixedStepAccumulator { FixedDeltaTime = 0.02f };
        Assert.Equal(3, acc.Advance(0.06f));
        Assert.Equal(0f, acc.Remainder);
    }

    [Fact]
    public void Advance_DefaultFixedDeltaTime_IsTwentyMs()
    {
        var acc = new FixedStepAccumulator();
        Assert.Equal(0.02f, acc.FixedDeltaTime);
        Assert.Equal(1, acc.Advance(0.02f));
        Assert.Equal(0f, acc.Remainder);
    }

    [Fact]
    public void FixedDeltaTime_Change_AppliesToSubsequentAdvances()
    {
        var acc = new FixedStepAccumulator { FixedDeltaTime = 0.02f };
        acc.Advance(0.015f);                        // 剩余 0.015
        acc.FixedDeltaTime = 0.01f;
        Assert.Equal(4, acc.Advance(0.025f));       // 0.015+0.025=0.04 → 4×0.01
        Assert.Equal(0f, acc.Remainder, 5);
    }
}
