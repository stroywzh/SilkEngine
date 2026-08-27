using SilkEngine.Math;
using SilkEngine.Render;

namespace SilkEngine.Tests.Render;

public class MaterialTests
{
    [Fact]
    public void SetFloat_RemovesSameNameFromVectorsAndMatrices()
    {
        var m = new MaterialLegacy { Name = "m" };
        m.SetVector3("x", new Vector3(1, 2, 3));
        m.SetMatrix4x4("x", new Matrix4x4());
        m.SetFloat("x", 5f);
        Assert.False(m.Vectors.ContainsKey("x"));
        Assert.False(m.Matrices.ContainsKey("x"));
        Assert.Equal(5f, m.Floats["x"]);
    }

    [Fact]
    public void SetVector3_RemovesSameNameFromFloatsAndMatrices()
    {
        var m = new MaterialLegacy { Name = "m" };
        m.SetFloat("x", 5f);
        m.SetMatrix4x4("x", new Matrix4x4());
        m.SetVector3("x", new Vector3(1, 2, 3));
        Assert.False(m.Floats.ContainsKey("x"));
        Assert.False(m.Matrices.ContainsKey("x"));
        Assert.Equal(new Vector3(1, 2, 3), m.Vectors["x"]);
    }

    [Fact]
    public void SetMatrix4x4_RemovesSameNameFromFloatsAndVectors()
    {
        var m = new MaterialLegacy { Name = "m" };
        m.SetFloat("x", 5f);
        m.SetVector3("x", new Vector3(1, 2, 3));
        var mat = new Matrix4x4();
        mat.M11 = 2f;
        m.SetMatrix4x4("x", mat);
        Assert.False(m.Floats.ContainsKey("x"));
        Assert.False(m.Vectors.ContainsKey("x"));
        Assert.Equal(2f, m.Matrices["x"][0]);
    }

    [Fact]
    public void Equals_MatrixValuesDifferent_ReturnsFalse()
    {
        var a = new MaterialLegacy { Name = "m" };
        a.SetMatrix4x4("mat", new Matrix4x4());
        var b = new MaterialLegacy { Name = "m" };
        var m = new Matrix4x4();
        m.M11 = 999f;
        b.SetMatrix4x4("mat", m);
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void Equals_MatrixValuesSame_ReturnsTrue()
    {
        var a = new MaterialLegacy { Name = "m" };
        a.SetMatrix4x4("mat", new Matrix4x4());
        var b = new MaterialLegacy { Name = "m" };
        b.SetMatrix4x4("mat", new Matrix4x4());
        Assert.True(a.Equals(b));
    }
}
