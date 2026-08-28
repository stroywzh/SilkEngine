using SilkEngine.Assets;
using SilkEngine.Assets.Binding;
using SilkEngine.Math;
using SilkEngine.Render;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Tests.Scene;

/// <summary>
/// 材质绑定解析（阶段 3 任务 2）：MaterialResolver 将运行时材质实例（默认参数 + 覆盖参数）
/// 解析为无资产语义的 RenderMaterialParameters；Rendering 契约不暴露 MaterialAsset。
/// </summary>
public class MaterialRenderBindingTests
{
    private static MaterialAsset CreateMaterialAsset()
        => new(
            new AssetId(Guid.NewGuid()),
            new AssetHandle<ShaderAsset>(new AssetId(Guid.NewGuid())),
            null,
            new MaterialParameterSnapshot([("Roughness", MaterialValue.Float(0.4f))]));

    [Fact]
    public void ResolveForRender_MergesDefaultsAndOverrides()
    {
        var materialAsset = CreateMaterialAsset();
        var material = materialAsset.ToInstance();
        material.SetFloat("Roughness", 0.9f);
        material.SetVector3("Tint", new Vector3(1, 0, 0));

        var bound = MaterialResolver.ResolveForRender(material, materialAsset.Defaults);

        Assert.Equal(0.9f, bound.GetFloat("Roughness"));
        Assert.Equal(new Vector3(1, 0, 0), bound.GetVector3("Tint"));
    }

    [Fact]
    public void ResolveForRender_OverridesOnly_WhenNoDefaultsProvided()
    {
        var material = new Material(new MaterialReference(new AssetId(Guid.NewGuid())));
        material.SetVector3("Tint", new Vector3(0, 1, 0));

        var bound = MaterialResolver.ResolveForRender(material);

        Assert.Equal(new Vector3(0, 1, 0), bound.GetVector3("Tint"));
        Assert.Throws<KeyNotFoundException>(() => bound.GetFloat("Roughness"));
    }

    [Fact]
    public void RenderParameters_DoNotExposeMaterialAsset()
    {
        var materialAsset = CreateMaterialAsset();
        var material = materialAsset.ToInstance();
        material.SetVector3("Tint", new Vector3(1, 0, 0));

        var bound = MaterialResolver.ResolveForRender(material, materialAsset.Defaults);

        Assert.DoesNotContain("MaterialAsset", bound.GetType().GetProperties().Select(p => p.PropertyType.Name));
    }

    [Fact]
    public void RenderMaterialParameters_GetVector3_TypeMismatchThrows()
    {
        var parameters = new RenderMaterialParameters(
            [("Tint", RenderParameterValue.Float(1f))]);

        Assert.Throws<KeyNotFoundException>(() => parameters.GetVector3("Tint"));
    }
}
