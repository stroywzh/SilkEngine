using SilkEngine.Render;

namespace SilkEngine.Tests;

public class MeshFactoryTests
{
    [Fact]
    public void CreateCube_HasCorrectVertexAndIndexCount()
    {
        var cube = MeshFactory.CreateCube(1f);
        Assert.NotNull(cube.Indices);
        Assert.Equal(36, cube.Indices!.Length);
        Assert.Equal(24, cube.VertexCount);
    }

    [Fact]
    public void CreatePlane_HasCorrectCounts()
    {
        var plane = MeshFactory.CreatePlane(1, 1);
        Assert.NotNull(plane.Indices);
        Assert.Equal(4, plane.VertexCount);
        Assert.Equal(6, plane.Indices!.Length);
    }
}
