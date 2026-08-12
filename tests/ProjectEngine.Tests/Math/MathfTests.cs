namespace SilkEngine.Tests.Math;

public class MathfTests
{
    [Fact]
    public void Deg2Rad_ConvertsCorrectly()
    {
        Assert.Equal(MathF.PI, 180f * SilkEngine.Math.Mathf.Deg2Rad, 1e-5f);
    }

    [Fact]
    public void Rad2Deg_ConvertsCorrectly()
    {
        Assert.Equal(180f, MathF.PI * SilkEngine.Math.Mathf.Rad2Deg, 1e-5f);
    }

    [Fact]
    public void Clamp_ValueWithinRange_ReturnsValue()
    {
        Assert.Equal(5f, SilkEngine.Math.Mathf.Clamp(5f, 0f, 10f));
    }

    [Fact]
    public void Clamp_ValueBelowMin_ReturnsMin()
    {
        Assert.Equal(0f, SilkEngine.Math.Mathf.Clamp(-5f, 0f, 10f));
    }

    [Fact]
    public void Clamp_ValueAboveMax_ReturnsMax()
    {
        Assert.Equal(10f, SilkEngine.Math.Mathf.Clamp(15f, 0f, 10f));
    }

    [Fact]
    public void Lerp_T0_ReturnsA()
    {
        Assert.Equal(2f, SilkEngine.Math.Mathf.Lerp(2f, 8f, 0f));
    }

    [Fact]
    public void Lerp_T1_ReturnsB()
    {
        Assert.Equal(8f, SilkEngine.Math.Mathf.Lerp(2f, 8f, 1f));
    }

    [Fact]
    public void Lerp_THalf_ReturnsMidpoint()
    {
        Assert.Equal(5f, SilkEngine.Math.Mathf.Lerp(2f, 8f, 0.5f));
    }

    [Fact]
    public void Abs_Negative_ReturnsPositive()
    {
        Assert.Equal(3.5f, SilkEngine.Math.Mathf.Abs(-3.5f));
    }

    [Fact]
    public void Abs_Positive_ReturnsSame()
    {
        Assert.Equal(3.5f, SilkEngine.Math.Mathf.Abs(3.5f));
    }
}
