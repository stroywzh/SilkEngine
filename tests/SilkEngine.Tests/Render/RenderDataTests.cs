using SilkEngine.Math;
using SilkEngine.Render;

namespace SilkEngine.Tests.Render;

public class RenderDataTests
{
    [Fact]
    public void Mesh_SameContent_Equals()
    {
        var a = new Mesh { Name = "C", Vertices = [1,2,3], Layout = [3] };
        var b = new Mesh { Name = "C", Vertices = [1,2,3], Layout = [3] };
        Assert.True(a.Equals(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Mesh_DifferentVertices_NotEqual()
    {
        var a = new Mesh { Name = "C", Vertices = [1,2,3], Layout = [3] };
        var b = new Mesh { Name = "C", Vertices = [4,5,6], Layout = [3] };
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void Mesh_DifferentLayout_NotEqual()
    {
        var a = new Mesh { Name = "C", Vertices = [1,2,3,4,5,6], Layout = [3,3] };
        var b = new Mesh { Name = "C", Vertices = [1,2,3,4,5,6], Layout = [6] };
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void Mesh_GetHashCode_Cached()
    {
        var m = new Mesh { Name = "X", Vertices = [1,2,3], Layout = [3] };
        int h1 = m.GetHashCode();
        int h2 = m.GetHashCode();
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void Material_SameContent_Equals()
    {
        var a = new MaterialLegacy { Name = "M" };
        a.SetFloat("f", 1f);
        var b = new MaterialLegacy { Name = "M" };
        b.SetFloat("f", 1f);
        Assert.True(a.Equals(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Material_DifferentFloats_NotEqual()
    {
        var a = new MaterialLegacy { Name = "M" };
        a.SetFloat("f", 1f);
        var b = new MaterialLegacy { Name = "M" };
        b.SetFloat("f", 2f);
        Assert.False(a.Equals(b));
    }

    [Fact]
    public void Material_Hash_ExcludesMutableDictionaries()
    {
        var a = new MaterialLegacy { Name = "M" };
        a.SetFloat("f", 1f);
        a.SetVector3("v", new Vector3(1, 2, 3));
        var b = new MaterialLegacy { Name = "M" };
        b.SetFloat("f", 2f);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());  // 字典差异不入哈希（旧实现：内容入哈希 → 不相等）
        Assert.False(a.Equals(b));                       // Equals 仍区分内容
    }

    [Fact]
    public void Material_Hash_StableAfterSetFloat()
    {
        var m = new MaterialLegacy { Name = "M" };
        int h1 = m.GetHashCode();
        m.SetFloat("f", 1f);                             // 可变字典变更不击穿缓存哈希
        m.SetMatrix4x4("m", Matrix4x4.Identity);
        Assert.Equal(h1, m.GetHashCode());
    }
}
