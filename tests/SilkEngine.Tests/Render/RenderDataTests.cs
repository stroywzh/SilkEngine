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
}
