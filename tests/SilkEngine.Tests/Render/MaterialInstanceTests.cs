using SilkEngine.Assets;
using SilkEngine.Math;
using SilkEngine.Render;

namespace SilkEngine.Tests.Render;

public class MaterialInstanceTests
{
    [Fact]
    public void TwoInstancesShareSourceButKeepOverridesIndependent()
    {
        var source = new MaterialReference(new AssetId(Guid.NewGuid()));
        var first = new Material(source);
        var second = new Material(source);

        first.SetFloat("Roughness", 0.2f);

        Assert.Equal(source, first.Source);
        Assert.Equal(source, second.Source);
        Assert.Equal(0.2f, first.Overrides.GetFloat("Roughness"));
        Assert.False(second.Overrides.TryGet("Roughness", out _));
    }

    [Fact]
    public void SettingParameterTypeRemovesOtherTypeWithSameName()
    {
        var material = new Material(new MaterialReference(new AssetId(Guid.NewGuid())));

        material.SetFloat("Value", 1f);
        material.SetVector3("Value", new Vector3(1f, 2f, 3f));

        Assert.False(material.Overrides.TryGetFloat("Value", out _));
        Assert.True(material.Overrides.TryGetVector3("Value", out var value));
        Assert.Equal(new Vector3(1f, 2f, 3f), value);
    }

    [Fact]
    public void SetMatrix4x4_StoresValue_AndRemovesSameNameOtherTypes()
    {
        var material = new Material(new MaterialReference(new AssetId(Guid.NewGuid())));
        material.SetFloat("Transform", 1f);
        var matrix = new Matrix4x4();
        matrix.M11 = 2f;
        matrix.M44 = 9f;

        material.SetMatrix4x4("Transform", matrix);

        Assert.False(material.Overrides.TryGetFloat("Transform", out _));
        Assert.False(material.Overrides.TryGetVector3("Transform", out _));
        Assert.True(material.Overrides.TryGetMatrix4x4("Transform", out var value));
        Assert.Equal(2f, value.M11);
        Assert.Equal(9f, value.M44);
    }

    [Fact]
    public void GetFloat_Missing_ThrowsKeyNotFoundException()
    {
        var material = new Material(new MaterialReference(new AssetId(Guid.NewGuid())));

        Assert.Throws<KeyNotFoundException>(() => material.Overrides.GetFloat("Missing"));
    }

    [Fact]
    public void SetParameter_EmptyOrWhitespaceName_ThrowsArgumentException()
    {
        var overrides = new MaterialOverrides();

        Assert.Throws<ArgumentException>(() => overrides.SetFloat("", 1f));
        Assert.Throws<ArgumentException>(() => overrides.SetVector3("   ", new Vector3()));
        Assert.Throws<ArgumentException>(() => overrides.SetMatrix4x4("\t", Matrix4x4.Identity));
    }

    [Fact]
    public void ClearOverrides_RemovesAllParameters_AndIncrementsVersion()
    {
        var overrides = new MaterialOverrides();
        overrides.SetFloat("A", 1f);
        overrides.SetVector3("B", new Vector3(1, 2, 3));
        int before = overrides.Version;

        overrides.ClearOverrides();

        Assert.Equal(0, overrides.Count);
        Assert.False(overrides.TryGet("A", out _));
        Assert.False(overrides.TryGet("B", out _));
        Assert.Equal(before + 1, overrides.Version);
    }

    [Fact]
    public void Clear_IsAliasForClearOverrides()
    {
        var overrides = new MaterialOverrides();
        overrides.SetFloat("A", 1f);

        overrides.Clear();

        Assert.Equal(0, overrides.Count);
        Assert.False(overrides.TryGet("A", out _));
    }

    [Fact]
    public void Version_IncrementsOnEverySetAndClear()
    {
        var overrides = new MaterialOverrides();
        Assert.Equal(0, overrides.Version);

        overrides.SetFloat("A", 1f);
        Assert.Equal(1, overrides.Version);

        overrides.SetFloat("A", 2f);
        Assert.Equal(2, overrides.Version);

        overrides.SetVector3("B", new Vector3(1, 2, 3));
        Assert.Equal(3, overrides.Version);

        overrides.ClearOverrides();
        Assert.Equal(4, overrides.Version);
    }

    [Fact]
    public void Snapshot_CopiesData_AndIsUnaffectedByLaterChanges()
    {
        var overrides = new MaterialOverrides();
        overrides.SetFloat("Roughness", 0.4f);

        var snapshot = overrides.Snapshot();
        overrides.SetFloat("Roughness", 0.9f);
        overrides.SetVector3("Color", new Vector3(1, 0, 0));

        Assert.Equal(1, snapshot.Count);
        Assert.Equal(0.4f, snapshot.GetFloat("Roughness"));
        Assert.False(snapshot.TryGet("Color", out _));
    }

    [Fact]
    public void MaterialParameterSnapshot_ConstructedFromParameterCollection()
    {
        var snapshot = new MaterialParameterSnapshot(
            [("Roughness", MaterialValue.Float(0.4f)), ("Offset", MaterialValue.Vector3(new Vector3(1f, 2f, 3f)))]
        );

        Assert.Equal(2, snapshot.Count);
        Assert.Equal(0.4f, snapshot.GetFloat("Roughness"));
        Assert.True(snapshot.TryGetVector3("Offset", out var offset));
        Assert.Equal(new Vector3(1f, 2f, 3f), offset);
    }

    [Fact]
    public void Snapshot_GetFloat_MissingOrTypeMismatch_ThrowsKeyNotFoundException()
    {
        var snapshot = new MaterialParameterSnapshot([("Roughness", MaterialValue.Float(0.4f))]);

        Assert.Throws<KeyNotFoundException>(() => snapshot.GetFloat("Missing"));
        Assert.Throws<KeyNotFoundException>(() => snapshot.GetVector3("Roughness"));
    }
}
