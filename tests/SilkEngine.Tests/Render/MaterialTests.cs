using SilkEngine.Math;
using SilkEngine.Render;

namespace SilkEngine.Tests.Render;

public class MaterialTests
{
    [Fact]
    public void Equals_MatrixValuesDifferent_ReturnsFalse()
    {
        var a = new Material { Name = "m" };
        a.SetMatrix4x4("mat", new Matrix4x4());
        var b = new Material { Name = "m" };
        var m = new Matrix4x4();
        m.M11 = 999f;
        b.SetMatrix4x4("mat", m);
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void Equals_MatrixValuesSame_ReturnsTrue()
    {
        var a = new Material { Name = "m" };
        a.SetMatrix4x4("mat", new Matrix4x4());
        var b = new Material { Name = "m" };
        b.SetMatrix4x4("mat", new Matrix4x4());
        Assert.True(a.Equals(b));
    }
}
