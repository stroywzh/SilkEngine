using SilkEngine.Assets;
using SilkEngine.Assets.Serialization;
using SilkEngine.Math;
using SilkEngine.Render;

namespace SilkEngine.Tests.Core.Assets;

/// <summary>内置资产序列化器测试：Texture/Mesh/Shader/Material 字段往返、类型/版本互拒与数据损坏语义</summary>
public class AssetSerializerTests
{
    [Fact]
    public void MaterialSerializer_PreservesHandlesAndDefaults()
    {
        var material = Fixtures.MaterialAssetWithDependencies();
        var serializer = new MaterialAssetSerializer();

        var record = serializer.Serialize(material);
        var restored = Assert.IsType<MaterialAsset>(
            serializer.Deserialize(record, new NoopReferenceResolver()));

        Assert.Equal(material.Shader, restored.Shader);
        Assert.Equal(material.MainTexture, restored.MainTexture);
        Assert.Equal(material.Defaults, restored.Defaults);
        Assert.DoesNotContain("Overrides", record.Data);
    }

    [Fact]
    public void Serializer_RejectsWrongTypeAndVersionWithBclExceptions()
    {
        var serializer = new TextureAssetSerializer();
        var record = Fixtures.SerializationRecord(type: "mesh", version: 99);

        Assert.Throws<NotSupportedException>(() => serializer.Deserialize(record,
            new NoopReferenceResolver()));
    }

    [Fact]
    public void TextureSerializer_RoundTripsFields()
    {
        var pixels = new byte[2 * 2 * 4];
        for (var i = 0; i < pixels.Length; i++)
            pixels[i] = (byte)i;
        var texture = new TextureAsset("checker", new ImageData(2, 2, pixels));
        var serializer = new TextureAssetSerializer();

        var record = serializer.Serialize(texture);
        var restored = Assert.IsType<TextureAsset>(serializer.Deserialize(record, new NoopReferenceResolver()));

        Assert.Equal("checker", restored.Name);
        Assert.Equal(2, restored.Data.Width);
        Assert.Equal(2, restored.Data.Height);
        Assert.Equal(pixels, restored.Data.RawBytes);
    }

    [Fact]
    public void MeshSerializer_RoundTripsFields()
    {
        var mesh = new MeshAsset("quad",
            [0f, 0f, 0f, 1f, 0f, 0f, 1f, 1f, 0f, 0f, 1f, 0f],
            [3],
            [0, 1, 2, 3]);
        var serializer = new MeshAssetSerializer();

        var record = serializer.Serialize(mesh);
        var restored = Assert.IsType<MeshAsset>(serializer.Deserialize(record, new NoopReferenceResolver()));

        Assert.Equal("quad", restored.Name);
        Assert.Equal(mesh.Vertices, restored.Vertices);
        Assert.Equal(mesh.Layout, restored.Layout);
        Assert.Equal(mesh.Indices, restored.Indices);
    }

    [Fact]
    public void MeshSerializer_RoundTripsNullIndices()
    {
        var mesh = new MeshAsset("nonindexed", [1f, 2f, 3f], [3], null);
        var serializer = new MeshAssetSerializer();

        var record = serializer.Serialize(mesh);
        var restored = Assert.IsType<MeshAsset>(serializer.Deserialize(record, new NoopReferenceResolver()));

        Assert.Null(restored.Indices);
    }

    [Fact]
    public void ShaderSerializer_RoundTripsSources()
    {
        var shader = new ShaderAsset("lit", "#version 330 core\nvoid main(){}", "#version 330 core\nvoid main(){}");
        var serializer = new ShaderAssetSerializer();

        var record = serializer.Serialize(shader);
        var restored = Assert.IsType<ShaderAsset>(serializer.Deserialize(record, new NoopReferenceResolver()));

        Assert.Equal("lit", restored.Name);
        Assert.Equal(shader.VertexSource, restored.VertexSource);
        Assert.Equal(shader.FragmentSource, restored.FragmentSource);
    }

    [Fact]
    public void Serializer_RejectsWrongObjectTypeWithArgumentException()
    {
        var serializer = new TextureAssetSerializer();

        Assert.Throws<ArgumentException>(() => serializer.Serialize(new ShaderAsset("s", "v", "f")));
    }

    [Fact]
    public void Serializer_RejectsRecordsOfOtherTypes()
    {
        var textureRecord = new TextureAssetSerializer().Serialize(
            new TextureAsset("t", new ImageData(1, 1, new byte[4])));
        var materialSerializer = new MaterialAssetSerializer();

        Assert.Throws<NotSupportedException>(() =>
            materialSerializer.Deserialize(textureRecord, new NoopReferenceResolver()));
    }

    [Fact]
    public void Serializer_CorruptData_ThrowsInvalidDataException()
    {
        var serializer = new ShaderAssetSerializer();
        var record = Fixtures.SerializationRecord(type: "shader") with { Data = "not-json" };
        var missingField = Fixtures.SerializationRecord(type: "shader") with { Data = "{\"name\":\"x\"}" };

        Assert.Throws<InvalidDataException>(() =>
            serializer.Deserialize(record, new NoopReferenceResolver()));
        Assert.Throws<InvalidDataException>(() =>
            serializer.Deserialize(missingField, new NoopReferenceResolver()));
    }
}
