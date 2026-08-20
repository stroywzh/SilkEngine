using SilkEngine.Math;

namespace SilkEngine.Tests.Math;

public class Vector2Tests
{
    [Fact]
    public void Constructor_SetsFields()
    {
        var v = new Vector2(1f, 2f);
        Assert.Equal(1f, v.X);
        Assert.Equal(2f, v.Y);
    }

    [Fact]
    public void Zero_IsAllZeros()
    {
        Assert.Equal(new Vector2(0, 0), Vector2.Zero);
    }

    [Fact]
    public void Magnitude_ComputesLength()
    {
        Assert.Equal(5f, new Vector2(3, 4).Magnitude, 1e-5f);
    }

    [Fact]
    public void MagnitudeSquared_ComputesSquaredLength()
    {
        Assert.Equal(25f, new Vector2(3, 4).MagnitudeSquared, 1e-5f);
    }

    [Fact]
    public void Dot_ComputesDotProduct()
    {
        Assert.Equal(2f, Vector2.Dot(new Vector2(1, 2), new Vector2(4, -1)));
    }

    [Fact]
    public void Distance_ComputesEuclideanDistance()
    {
        Assert.Equal(5f, Vector2.Distance(new Vector2(0, 0), new Vector2(3, 4)), 1e-5f);
    }

    [Fact]
    public void Lerp_T0_ReturnsA()
    {
        Assert.Equal(new Vector2(1, 2), Vector2.Lerp(new Vector2(1, 2), new Vector2(4, 5), 0f));
    }

    [Fact]
    public void Lerp_T1_ReturnsB()
    {
        Assert.Equal(new Vector2(4, 5), Vector2.Lerp(new Vector2(1, 2), new Vector2(4, 5), 1f));
    }

    [Fact]
    public void Operator_Add()
    {
        Assert.Equal(new Vector2(5, 7), new Vector2(1, 2) + new Vector2(4, 5));
    }

    [Fact]
    public void Operator_Subtract()
    {
        Assert.Equal(new Vector2(-3, -3), new Vector2(1, 2) - new Vector2(4, 5));
    }

    [Fact]
    public void Operator_UnaryNegation()
    {
        Assert.Equal(new Vector2(-1, -2), -new Vector2(1, 2));
    }

    [Fact]
    public void Operator_MultiplyScalar()
    {
        Assert.Equal(new Vector2(2, 4), new Vector2(1, 2) * 2f);
        Assert.Equal(new Vector2(2, 4), 2f * new Vector2(1, 2));
    }

    [Fact]
    public void Operator_Equality()
    {
        Assert.True(new Vector2(1, 2) == new Vector2(1, 2));
        Assert.True(new Vector2(1, 2) != new Vector2(4, 5));
    }

    [Fact]
    public void GetHashCode_EqualVectors_EqualHashCodes()
    {
        Assert.Equal(new Vector2(1, 2).GetHashCode(), new Vector2(1, 2).GetHashCode());
    }
}
