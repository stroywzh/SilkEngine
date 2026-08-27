using System.Linq;
using SilkEngine.Assets;
using SilkEngine.Render;

namespace SilkEngine.Tests;

public class MeshFactoryTests
{
    private static int VertexCount(MeshAsset mesh) => mesh.Vertices.Length / mesh.Layout.Sum();

    [Fact]
    public void CreateCube_HasCorrectVertexAndIndexCount()
    {
        var cube = MeshFactory.CreateCube(1f);
        Assert.NotNull(cube.Indices);
        Assert.Equal(36, cube.Indices!.Length);
        Assert.Equal(24, VertexCount(cube));
    }

    [Fact]
    public void CreatePlane_HasCorrectCounts()
    {
        var plane = MeshFactory.CreatePlane(1, 1);
        Assert.NotNull(plane.Indices);
        Assert.Equal(4, VertexCount(plane));
        Assert.Equal(6, plane.Indices!.Length);
    }
}
