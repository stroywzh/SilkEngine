using System.IO;
using System.Text;
using SilkEngine.Assets;
using SilkEngine.Assets.Importer;

namespace SilkEngine.Tests.Assets;

/// <summary>OBJ 网格导入器测试：v/vt/vn/f 解析、负索引换算、越界与非三角面错误语义</summary>
public class ObjMeshImporterTests
{
    [Fact]
    public void ObjImporter_ImportsPositionsNormalsUvAndIndices()
    {
        const string obj = "v 0 0 0\nv 1 0 0\nv 0 1 0\n"
            + "vt 0 0\nvt 1 0\nvt 0 1\n"
            + "vn 0 0 1\nf 1/1/1 2/2/1 3/3/1\n";

        var result = new ObjMeshImporter().Import(
            Encoding.UTF8.GetBytes(obj), new AssetImportContext("Meshes/Cube.obj", null));

        var mesh = Assert.IsType<MeshAsset>(result.Payload);
        Assert.Equal(new[] { 3, 3, 2 }, mesh.Layout);
        Assert.Equal(3, mesh.Indices!.Length);
        Assert.Equal(3 * 8, mesh.Vertices.Length);
        Assert.Equal("Cube", mesh.Name);
    }

    [Fact]
    public void ObjImporter_NegativeIndicesResolveFromEnd()
    {
        const string obj = "v 0 0 0\nv 1 0 0\nv 0 1 0\nv 1 1 0\n"
            + "vt 0 0\nvt 1 0\nvt 0 1\n"
            + "vn 0 0 1\nvn 0 0 1\nvn 0 0 1\nf -1/-1/-1 -2/-2/-2 -3/-3/-3\n";

        var result = new ObjMeshImporter().Import(
            Encoding.UTF8.GetBytes(obj), new AssetImportContext("Meshes/Cube.obj", null));

        var mesh = Assert.IsType<MeshAsset>(result.Payload);
        // 负索引按 OBJ 规则换算：-1 → 倒数第 1（v4）、-2 → v3、-3 → v2；顶点数据按引用顺序展开，网格索引顺序编号
        Assert.Equal(3, mesh.Indices!.Length);
        Assert.Equal([0, 1, 2], mesh.Indices);
        Assert.Equal(1f, mesh.Vertices[0]);
        Assert.Equal(1f, mesh.Vertices[1]);
        Assert.Equal(0f, mesh.Vertices[8]);
        Assert.Equal(1f, mesh.Vertices[9]);
        Assert.Equal(1f, mesh.Vertices[16]);
        Assert.Equal(0f, mesh.Vertices[17]);
    }

    [Fact]
    public void ObjImporter_IndexOutOfRange_ThrowsWithPath()
    {
        const string obj = "v 0 0 0\nv 1 0 0\nv 0 1 0\n"
            + "vt 0 0\nvt 1 0\nvt 0 1\n"
            + "vn 0 0 1\nf 1/1/1 2/2/1 9/3/1\n";
        var importer = new ObjMeshImporter();

        var exception = Assert.Throws<InvalidDataException>(() => importer.Import(
            Encoding.UTF8.GetBytes(obj), new AssetImportContext("Meshes/Cube.obj", null)));

        Assert.Contains("Meshes/Cube.obj", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ObjImporter_NonTriangleFace_ThrowsWithPath()
    {
        const string obj = "v 0 0 0\nv 1 0 0\nv 0 1 0\nv 1 1 0\n"
            + "vt 0 0\nvt 1 0\nvt 0 1\n"
            + "vn 0 0 1\nf 1/1/1 2/2/1 3/3/1 4/1/1\n";
        var importer = new ObjMeshImporter();

        var exception = Assert.Throws<InvalidDataException>(() => importer.Import(
            Encoding.UTF8.GetBytes(obj), new AssetImportContext("Meshes/Cube.obj", null)));

        Assert.Contains("Meshes/Cube.obj", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ObjImporter_PositionOnlyFaces_AreRejected()
    {
        const string obj = "v 0 0 0\nv 1 0 0\nv 0 1 0\n"
            + "vt 0 0\nvt 1 0\nvt 0 1\n"
            + "vn 0 0 1\nf 1 2 3\n";
        var importer = new ObjMeshImporter();

        var exception = Assert.Throws<InvalidDataException>(() => importer.Import(
            Encoding.UTF8.GetBytes(obj), new AssetImportContext("Meshes/Cube.obj", null)));

        Assert.Contains("Meshes/Cube.obj", exception.Message, StringComparison.Ordinal);
    }
}