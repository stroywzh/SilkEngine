using SilkEngine.Math;

namespace SilkEngine.Tests.Math;

public class QuaternionTests
{
    [Fact]
    public void Identity_HasNoRotation()
    {
        Assert.Equal(new Quaternion(0, 0, 0, 1), Quaternion.Identity);
    }

    [Fact]
    public void Euler_Pitch90_RotatesForwardToDown()
    {
        var q = Quaternion.Euler(90, 0, 0);
        var result = q * Vector3.Forward;
        Assert.Equal(0f, result.X, 1e-4f);
        Assert.Equal(-1f, result.Y, 1e-4f);
        Assert.Equal(0f, result.Z, 1e-4f);
    }

    [Fact]
    public void Euler_Yaw90_RotatesForwardToRight()
    {
        var q = Quaternion.Euler(0, 90, 0);
        var result = q * Vector3.Forward;
        Assert.Equal(1f, result.X, 1e-4f);
        Assert.Equal(0f, result.Y, 1e-4f);
        Assert.Equal(0f, result.Z, 1e-4f);
    }

    [Fact]
    public void Multiply_Identity_ReturnsSame()
    {
        var q = Quaternion.Euler(45, 30, 15);
        Assert.Equal(q, q * Quaternion.Identity);
        Assert.Equal(q, Quaternion.Identity * q);
    }

    [Fact]
    public void Equality_Works()
    {
        var a = new Quaternion(1, 2, 3, 4);
        var b = new Quaternion(1, 2, 3, 4);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
