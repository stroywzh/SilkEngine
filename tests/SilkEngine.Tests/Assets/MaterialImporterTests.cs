using System.IO;
using System.Text;
using SilkEngine.Assets;
using SilkEngine.Assets.Importer;
using SilkEngine.Math;

namespace SilkEngine.Tests.Assets;

/// <summary>材质 .asset 导入器测试：JSON 依赖与参数解析、Name 派生与依赖类型</summary>
public class MaterialImporterTests
{
    [Fact]
    public void MaterialImporter_ReadsJsonReferencesAndParameters()
    {
        const string json = "{\"schema\":1,\"type\":\"material\","
            + "\"shader\":\"Shaders/Unlit.hlsl\","
            + "\"texture\":\"Textures/ShoreKeeper1.png\","
            + "\"mesh\":\"Meshes/Cube.obj\","
            + "\"parameters\":{\"BaseColor\":[1,1,1]}}";

        var result = new MaterialImporter().Import(
            Encoding.UTF8.GetBytes(json), new AssetImportContext("Materials/Cube.asset", null));

        var material = Assert.IsAssignableFrom<MaterialAsset>(result.Payload);
        Assert.Equal(3, result.Dependencies.Count);
        Assert.True(material.Defaults.TryGetVector3("BaseColor", out _));
    }

    [Fact]
    public void MaterialImporter_NameComesFromPathFileName()
    {
        const string json = "{\"schema\":1,\"type\":\"material\",\"shader\":\"Shaders/Unlit.hlsl\"}";

        var result = new MaterialImporter().Import(
            Encoding.UTF8.GetBytes(json), new AssetImportContext("Materials/ShoreKeeper.asset", null));

        var material = Assert.IsAssignableFrom<MaterialAsset>(result.Payload);
        Assert.Equal("ShoreKeeper", material.Name);
    }

    [Fact]
    public void MaterialImporter_OptionalReferencesAreOnlyAddedWhenPresent()
    {
        const string json = "{\"schema\":1,\"type\":\"material\",\"shader\":\"Shaders/Unlit.hlsl\"}";

        var result = new MaterialImporter().Import(
            Encoding.UTF8.GetBytes(json), new AssetImportContext("Materials/Cube.asset", null));

        var dependency = Assert.Single(result.Dependencies);
        Assert.Equal("Shaders/Unlit.hlsl", dependency.LogicalPath);
        Assert.Equal(AssetImporterRegistry.ShaderAssetTypeId, dependency.ExpectedType);
    }

    [Fact]
    public void MaterialImporter_ParsesFloatAndMatrixParameters()
    {
        const string json = "{\"schema\":1,\"type\":\"material\",\"shader\":\"Shaders/Unlit.hlsl\","
            + "\"parameters\":{\"Roughness\":0.4,\"World\":[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]}}";

        var result = new MaterialImporter().Import(
            Encoding.UTF8.GetBytes(json), new AssetImportContext("Materials/Cube.asset", null));

        var material = Assert.IsAssignableFrom<MaterialAsset>(result.Payload);
        Assert.True(material.Defaults.TryGetFloat("Roughness", out var roughness));
        Assert.Equal(0.4f, roughness);
        Assert.True(material.Defaults.TryGetMatrix4x4("World", out var world));
        Assert.Equal(1f, world.M44);
    }

    [Fact]
    public void MaterialImporter_MissingShaderReference_Throws()
    {
        const string json = "{\"schema\":1,\"type\":\"material\","
            + "\"texture\":\"Textures/A.png\",\"mesh\":\"Meshes/Cube.obj\"}";
        var importer = new MaterialImporter();

        var exception = Assert.Throws<InvalidDataException>(() => importer.Import(
            Encoding.UTF8.GetBytes(json), new AssetImportContext("Materials/Cube.asset", null)));

        Assert.Contains("Materials/Cube.asset", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MaterialImporter_MismatchedSchema_Throws()
    {
        const string json = "{\"schema\":2,\"type\":\"material\",\"shader\":\"Shaders/Unlit.hlsl\"}";
        var importer = new MaterialImporter();

        var exception = Assert.Throws<InvalidDataException>(() => importer.Import(
            Encoding.UTF8.GetBytes(json), new AssetImportContext("Materials/Cube.asset", null)));

        Assert.Contains("schema", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}