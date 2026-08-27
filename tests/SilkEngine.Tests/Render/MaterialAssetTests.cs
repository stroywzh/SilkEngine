using SilkEngine.Assets;
using SilkEngine.Render;

namespace SilkEngine.Tests.Render;

public class MaterialAssetTests
{
    [Fact]
    public void MaterialAsset_StoresDependencyHandlesAndImmutableDefaults()
    {
        var shader = new AssetHandle<ShaderAsset>(new AssetId(Guid.NewGuid()));
        var texture = new AssetHandle<TextureAsset>(new AssetId(Guid.NewGuid()));
        var asset = new MaterialAsset(
            new AssetId(Guid.NewGuid()),
            shader,
            texture,
            new MaterialParameterSnapshot([("Roughness", MaterialValue.Float(0.4f))]));

        Assert.Equal(shader, asset.Shader);
        Assert.Equal(texture, asset.MainTexture);
        Assert.Equal(0.4f, asset.Defaults.GetFloat("Roughness"));
    }

    [Fact]
    public void MaterialAsset_DoesNotExposeRuntimeOverrides()
    {
        var properties = typeof(MaterialAsset).GetProperties();
        Assert.DoesNotContain(properties, p => p.Name == "Overrides");
    }

    [Fact]
    public void MaterialAsset_DefaultsAreIsolatedFromCallerCollection()
    {
        var parameters = new List<(string Name, MaterialValue Value)>
        {
            ("Roughness", MaterialValue.Float(0.4f)),
        };
        var asset = new MaterialAsset(
            new AssetId(Guid.NewGuid()),
            new AssetHandle<ShaderAsset>(new AssetId(Guid.NewGuid())),
            null,
            new MaterialParameterSnapshot(parameters));

        parameters[0] = ("Roughness", MaterialValue.Float(0.9f));

        Assert.Equal(0.4f, asset.Defaults.GetFloat("Roughness"));
        Assert.Equal(1, asset.Defaults.Count);
    }

    [Fact]
    public void MaterialAsset_StoresSourceRevision()
    {
        var asset = new MaterialAsset(
            new AssetId(Guid.NewGuid()),
            new AssetHandle<ShaderAsset>(new AssetId(Guid.NewGuid())),
            null,
            new MaterialParameterSnapshot([]),
            revision: 7);

        Assert.Equal(7UL, asset.Revision);
    }
}
