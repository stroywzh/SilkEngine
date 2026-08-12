using SilkEngine.Math;

namespace SilkEngine.Tests.Math;

public class Matrix4x4Tests
{
    [Fact]
    public void Identity_LeavesVectorUnchanged()
    {
        var v = new Vector3(1, 2, 3);
        var result = Matrix4x4.Identity * v;
        Assert.Equal(v, result);
    }

    [Fact]
    public void CreateTranslation_MovesVector()
    {
        var t = Matrix4x4.CreateTranslation(new Vector3(5, 0, 0));
        Assert.Equal(new Vector3(5, 0, 0), t * Vector3.Zero);
    }

    [Fact]
    public void CreateScale_ScalesVector()
    {
        var s = Matrix4x4.CreateScale(new Vector3(2, 3, 4));
        Assert.Equal(new Vector3(2, 3, 4), s * new Vector3(1, 1, 1));
    }

    [Fact]
    public void LookAt_AtOrigin_ViewMatrixCorrect()
    {
        var view = Matrix4x4.CreateLookAt(new Vector3(0, 0, -10), Vector3.Zero, Vector3.Up);
        var result = view * new Vector3(0, 0, -10);
        Assert.Equal(Vector3.Zero, result);
    }

    [Fact]
    public void Multiply_ComposesTransformations()
    {
        var t = Matrix4x4.CreateTranslation(new Vector3(5, 0, 0));
        var s = Matrix4x4.CreateScale(new Vector3(2, 1, 1));
        var ts = t * s;
        Assert.Equal(new Vector3(7, 0, 0), ts * new Vector3(1, 0, 0));
    }

    [Fact]
    public void Transpose_SwapsRowsAndColumns()
    {
        var m = new Matrix4x4 { M11=1,M12=2,M13=3,M14=4, M21=5,M22=6,M23=7,M24=8, M31=9,M32=10,M33=11,M34=12, M41=13,M42=14,M43=15,M44=16 };
        var t = m.Transposed;
        Assert.Equal(m.M12, t.M21);
        Assert.Equal(m.M21, t.M12);
        Assert.Equal(m.M11, t.M11);
        Assert.Equal(m.M44, t.M44);
    }

    [Fact]
    public void Perspective_TransformsForwardPoint()
    {
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI/4f, 1.0f, 0.1f, 100f);
        var result = proj * new Vector3(0, 0, 5);
        Assert.True(result.Z > 0);
    }

    [Fact]
    public void LookAt_NonAxisAligned_RotatesIntoViewSpace()
    {
        // 相机在 (10,0,0) 看原点：right=(0,0,1), u=(0,1,0), fwd=(-1,0,0)
        var view = Matrix4x4.CreateLookAt(new Vector3(10, 0, 0), Vector3.Zero, Vector3.Up);
        // 世界点 (0,0,5)：视空间 x' = right·p = 5（当前 buggy 实现得 -5）
        var p = view * new Vector3(0, 0, 5);
        Assert.Equal(5f, p.X, 3);
        Assert.Equal(0f, p.Y, 3);
        Assert.Equal(10f, p.Z, 3);   // fwd·(p-eye) = (-1,0,0)·(-10,0,5) = 10
    }

    [Fact]
    public void Perspective_NearAndFar_MapToZeroAndOne()
    {
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 2f, 1.0f, 0.1f, 100f);
        Assert.Equal(0f, (proj * new Vector3(0, 0, 0.1f)).Z, 3);
        Assert.Equal(1f, (proj * new Vector3(0, 0, 100f)).Z, 3);
        // 透视深度非线性：z'=0.5 出现在 z=2·near·far/(near+far)≈0.2 处（z=50 处已≈1.0）
        Assert.Equal(0.5f, (proj * new Vector3(0, 0, 0.2f)).Z, 2);
    }

    [Fact]
    public void Orthographic_NearAndFar_MapToZeroAndOne()
    {
        var ortho = Matrix4x4.CreateOrthographic(10f, 10f, 0.1f, 100f);
        Assert.Equal(0f, (ortho * new Vector3(0, 0, 0.1f)).Z, 3);
        Assert.Equal(1f, (ortho * new Vector3(0, 0, 100f)).Z, 3);
    }
}
