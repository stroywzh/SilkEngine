using System.IO;
using System.Text;
using SilkEngine.Assets;
using SilkEngine.Assets.Importer;

namespace SilkEngine.Tests.Assets;

/// <summary>HLSL 着色器导入器测试：入口函数校验、返回语义校验与不可变单源码载荷</summary>
public class ShaderImporterTests
{
    [Fact]
    public void HlslImporter_RequiresVertAndFrag()
    {
        var importer = new ShaderImporter();
        var source = Encoding.UTF8.GetBytes("float4 vert() : SV_Position { return 0; }");

        var exception = Assert.Throws<InvalidDataException>(() => importer.Import(
            source, new AssetImportContext("Shaders/Unlit.hlsl", null)));

        Assert.Contains("frag", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HlslImporter_RequiresVertWhenFragmentOnly()
    {
        var importer = new ShaderImporter();
        var source = Encoding.UTF8.GetBytes("float4 frag() : SV_Target { return 1; }");

        var exception = Assert.Throws<InvalidDataException>(() => importer.Import(
            source, new AssetImportContext("Shaders/Unlit.hlsl", null)));

        Assert.Contains("vert", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HlslImporter_ReturnsImmutableSingleSourceAsset()
    {
        const string source = "float4 vert(float3 p : POSITION) : SV_Position { return float4(p, 1); }\n"
            + "float4 frag() : SV_Target { return 1; }";
        var result = new ShaderImporter().Import(
            Encoding.UTF8.GetBytes(source), new AssetImportContext("Shaders/Unlit.hlsl", null));

        var shader = Assert.IsType<ShaderAsset>(result.Payload);
        Assert.Equal(source, shader.Source);
        Assert.Equal("vert", shader.VertexEntryPoint);
        Assert.Equal("frag", shader.FragmentEntryPoint);
        Assert.Equal("sm_6_0", shader.Profile);
    }

    [Fact]
    public void HlslImporter_RejectsWrongReturnSemantics()
    {
        const string source = "float4 vert() : SV_Target { return 0; }\n"
            + "float4 frag() : SV_Target { return 1; }";
        var importer = new ShaderImporter();

        var exception = Assert.Throws<InvalidDataException>(() => importer.Import(
            Encoding.UTF8.GetBytes(source), new AssetImportContext("Shaders/Unlit.hlsl", null)));

        Assert.Contains("SV_Position", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void HlslImporter_IgnoresEntryNamesInsideComments()
    {
        const string source = "// float4 frag() : SV_Target { return 1; }\n"
            + "/* float4 frag() : SV_Target { return 1; } */\n"
            + "float4 vert() : SV_Position { return 0; }";
        var importer = new ShaderImporter();

        var exception = Assert.Throws<InvalidDataException>(() => importer.Import(
            Encoding.UTF8.GetBytes(source), new AssetImportContext("Shaders/Unlit.hlsl", null)));

        Assert.Contains("frag", exception.Message, StringComparison.Ordinal);
    }
}