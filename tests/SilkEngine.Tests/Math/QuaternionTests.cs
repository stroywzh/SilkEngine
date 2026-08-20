using System;
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

    [Fact]
    public void Rotate_NonUnitQuaternion_MatchesNormalized()
    {
        var q = new Quaternion(1, 2, 3, 4);
        var v = new Vector3(1, 0, 0);
        var expected = q.Normalize() * v;
        var actual = q * v;
        Assert.Equal(expected.X, actual.X, 4);
        Assert.Equal(expected.Y, actual.Y, 4);
        Assert.Equal(expected.Z, actual.Z, 4);
    }

    [Fact]
    public void Inverse_NonUnit_ConjugateOverNormSquared()
    {
        var q = new Quaternion(1, 2, 3, 4);
        var expected = new Quaternion(-1f / 30f, -2f / 30f, -3f / 30f, 4f / 30f);
        Assert.Equal(expected.X, q.Inverse.X, 6);
        Assert.Equal(expected.Y, q.Inverse.Y, 6);
        Assert.Equal(expected.Z, q.Inverse.Z, 6);
        Assert.Equal(expected.W, q.Inverse.W, 6);

        var id = q * q.Inverse;
        Assert.Equal(1f, id.W, 1e-4f);
        Assert.Equal(0f, id.X, 1e-4f);
        Assert.Equal(0f, id.Y, 1e-4f);
        Assert.Equal(0f, id.Z, 1e-4f);
    }

    [Fact]
    public void Inverse_Zero_ReturnsIdentity()
    {
        Assert.Equal(Quaternion.Identity, new Quaternion(0, 0, 0, 0).Inverse);
    }

    [Fact]
    public void Normalize_Zero_ReturnsIdentity()
    {
        Assert.Equal(Quaternion.Identity, new Quaternion(0, 0, 0, 0).Normalize());
    }

    [Fact]
    public void Normalize_ScalesToUnitLength()
    {
        var q = new Quaternion(1, 2, 3, 4);
        var mag = MathF.Sqrt(30f);
        var n = q.Normalize();
        Assert.Equal(1f / mag, n.X, 6);
        Assert.Equal(2f / mag, n.Y, 6);
        Assert.Equal(3f / mag, n.Z, 6);
        Assert.Equal(4f / mag, n.W, 6);
    }
}
