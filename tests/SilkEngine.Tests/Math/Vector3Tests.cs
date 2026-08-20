using SilkEngine.Math;

namespace SilkEngine.Tests.Math;

public class Vector3Tests
{
    [Fact]
    public void Constructor_SetsFields()
    {
        var v = new Vector3(1f, 2f, 3f);
        Assert.Equal(1f, v.X);
        Assert.Equal(2f, v.Y);
        Assert.Equal(3f, v.Z);
    }

    [Fact]
    public void Zero_IsAllZeros()
    {
        Assert.Equal(new Vector3(0, 0, 0), Vector3.Zero);
    }

    [Fact]
    public void Forward_IsPositiveZ()
    {
        Assert.Equal(new Vector3(0, 0, 1), Vector3.Forward);
    }

    [Fact]
    public void Up_IsPositiveY()
    {
        Assert.Equal(new Vector3(0, 1, 0), Vector3.Up);
    }

    [Fact]
    public void Right_IsPositiveX()
    {
        Assert.Equal(new Vector3(1, 0, 0), Vector3.Right);
    }

    [Fact]
    public void Magnitude_ComputesLength()
    {
        Assert.Equal(5f, new Vector3(3, 4, 0).Magnitude, 1e-5f);
    }

    [Fact]
    public void Normalized_ReturnsUnitVector()
    {
        var n = new Vector3(3, 0, 0).Normalized;
        Assert.Equal(1f, n.Magnitude, 1e-5f);
        Assert.Equal(new Vector3(1, 0, 0), n);
    }

    [Fact]
    public void Normalized_ZeroVector_ReturnsZero()
    {
        Assert.Equal(Vector3.Zero, Vector3.Zero.Normalized);
    }

    [Fact]
    public void Normalized_NonZero_UnitLength()
    {
        var v = new Vector3(3, 4, 0);
        Assert.Equal(1f, v.Normalized.Magnitude, 5);
    }

    [Fact]
    public void Dot_ComputesDotProduct()
    {
        Assert.Equal(11f, Vector3.Dot(new Vector3(1, 2, 3), new Vector3(4, -1, 3)));
    }

    [Fact]
    public void Cross_ComputesCrossProduct_LeftHanded()
    {
        var result = Vector3.Cross(Vector3.Right, Vector3.Up);
        Assert.Equal(Vector3.Forward, result);
    }

    [Fact]
    public void Distance_ComputesEuclideanDistance()
    {
        Assert.Equal(5f, Vector3.Distance(new Vector3(0, 0, 0), new Vector3(3, 4, 0)), 1e-5f);
    }

    [Fact]
    public void Lerp_T0_ReturnsA()
    {
        Assert.Equal(new Vector3(1, 2, 3), Vector3.Lerp(new Vector3(1, 2, 3), new Vector3(4, 5, 6), 0f));
    }

    [Fact]
    public void Lerp_T1_ReturnsB()
    {
        Assert.Equal(new Vector3(4, 5, 6), Vector3.Lerp(new Vector3(1, 2, 3), new Vector3(4, 5, 6), 1f));
    }

    [Fact]
    public void Operator_Add()
    {
        Assert.Equal(new Vector3(5, 7, 9), new Vector3(1, 2, 3) + new Vector3(4, 5, 6));
    }

    [Fact]
    public void Operator_MultiplyScalar()
    {
        Assert.Equal(new Vector3(2, 4, 6), new Vector3(1, 2, 3) * 2f);
    }

    [Fact]
    public void Operator_Equality()
    {
        Assert.True(new Vector3(1, 2, 3) == new Vector3(1, 2, 3));
        Assert.True(new Vector3(1, 2, 3) != new Vector3(4, 5, 6));
    }

    [Fact]
    public void GetHashCode_EqualVectors_EqualHashCodes()
    {
        Assert.Equal(new Vector3(1, 2, 3).GetHashCode(), new Vector3(1, 2, 3).GetHashCode());
    }
}
