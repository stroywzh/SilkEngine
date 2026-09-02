using SilkEngine.Assets;
using SilkEngine.Host;
using SilkEngine.Math;
using SilkEngine.Render;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Scene;

namespace SilkEngine.Tests.Scene;

using Scene = SilkEngine.Scene.Scene;

/// <summary>
/// 材质资产消费面（任务 8）：RendererBase 标准绑定只接受 Material，
/// defaults 经生产绑定路径（MaterialBinding + MaterialResolver）合并；
/// 实例覆盖隔离；同名 defaults/overrides 类型不一致与渲染不支持类型显式 Failed；
/// 渲染参数不含资产身份。
/// </summary>
[Collection("Assets")]
public class MaterialAssetConsumptionTests
{
    [Fact]
    public void Renderer_ConsumesMaterialDefaultsWithoutPublicShaderProperty()
    {
        using var fixture = SceneTestFixture.Headless();
        var scene = fixture.CreateLoadedScene("Cube");
        var cube = scene.CreateGameObject("Cube");
        var renderer = cube.AddComponent<MeshRenderer>();
        var material = fixture.RegisterMaterialAsset();

        renderer.Material = material;
        var parameters = fixture.ResolveMaterial(renderer);

        Assert.True(parameters.TryGetVector3("BaseColor", out var color));
        Assert.Equal(new Vector3(1, 1, 1), color);
        Assert.DoesNotContain(typeof(RendererBase).GetProperties(), property => property.Name == "Shader");
    }

    [Fact]
    public void MaterialInstanceOverride_DoesNotMutateSharedMaterialDefaults()
    {
        var material = MaterialTestData.CreateAssetWithColor(new Vector3(1, 1, 1));
        var first = material.ToInstance();
        var second = material.ToInstance();

        first.SetVector3("BaseColor", new Vector3(1, 0, 0));

        Assert.Equal(new Vector3(1, 1, 1), material.Defaults.GetVector3("BaseColor"));
        Assert.False(second.Overrides.TryGet("BaseColor", out _));
    }

    [Fact]
    public void Binding_TypeMismatchBetweenDefaultsAndOverrides_Fails()
    {
        using var fixture = SceneTestFixture.Headless();
        var shader = fixture.Assets.RegisterTransient(new ShaderAsset("S", "void main() {}", "vert", "frag"));
        var asset = MaterialTestData.CreateAssetWithColor(new Vector3(1, 1, 1), shader);
        var handle = fixture.Assets.RegisterTransient(asset);
        var instance = new Material(new MaterialReference(handle.Id));
        instance.SetFloat("BaseColor", 2f);

        var result = new MaterialBinding(fixture.Assets).Resolve(instance);

        Assert.Equal(MaterialBindingState.Failed, result.State);
        Assert.Contains("BaseColor", result.Error ?? "");
        Assert.Contains("Vector3", result.Error);
        Assert.Contains("Float", result.Error);
    }

    [Fact]
    public void Binding_UnsupportedRenderParameterType_ReturnsFailed()
    {
        using var fixture = SceneTestFixture.Headless();
        var shader = fixture.Assets.RegisterTransient(new ShaderAsset("S", "void main() {}", "vert", "frag"));
        var asset = new MaterialAsset(
            new AssetId(Guid.NewGuid()),
            shader,
            null,
            new MaterialParameterSnapshot([("XForm", MaterialValue.Matrix4x4(Matrix4x4.Identity))]));
        var handle = fixture.Assets.RegisterTransient(asset);
        var instance = new Material(new MaterialReference(handle.Id));

        var result = new MaterialBinding(fixture.Assets).Resolve(instance);

        Assert.Equal(MaterialBindingState.Failed, result.State);
        Assert.Contains("XForm", result.Error ?? "");
        Assert.Contains("Matrix4x4", result.Error);
    }

    [Fact]
    public void RendererParameters_BoundThroughMaterial_DoNotExposeAssetIdentity()
    {
        using var fixture = SceneTestFixture.Headless();
        var scene = fixture.CreateLoadedScene("Cube");
        var renderer = scene.CreateGameObject("Cube").AddComponent<MeshRenderer>();
        renderer.Material = fixture.RegisterMaterialAsset();

        var parameters = fixture.ResolveMaterial(renderer);

        var reachableTypeNames = parameters.GetType()
            .GetProperties()
            .Select(property => property.PropertyType.Name)
            .Concat(parameters.Enumerate().Select(entry => entry.Value.GetType().Name));
        Assert.DoesNotContain("MaterialAsset", reachableTypeNames);
        Assert.DoesNotContain(reachableTypeNames, name => name.StartsWith("AssetHandle", StringComparison.Ordinal));
        Assert.DoesNotContain(reachableTypeNames, name => name.Contains("AssetId", StringComparison.Ordinal));
    }

    /// <summary>Headless 引擎测试夹具（本文件 private）：装配 AssetManager + 已加载场景</summary>
    private sealed class SceneTestFixture : IDisposable
    {
        private readonly EngineHost _host;

        private SceneTestFixture(EngineHost host) => _host = host;

        public static SceneTestFixture Headless()
        {
            var host = EngineHost.Create(builder => builder.UseHeadlessForTests());
            host.Initialize();
            return new SceneTestFixture(host);
        }

        public AssetManager Assets => _host.AssetManager;

        public Scene CreateLoadedScene(string name)
        {
            var scene = _host.SceneManager.Create(name);
            _host.SceneManager.LoadScene(scene);
            return scene;
        }

        public Material RegisterMaterialAsset()
        {
            var shader = _host.AssetManager.RegisterTransient(new ShaderAsset("Unlit", "void main() {}", "vert", "frag"));
            var asset = MaterialTestData.CreateAssetWithColor(new Vector3(1, 1, 1), shader);
            var handle = _host.AssetManager.RegisterTransient(asset);
            return new Material(new MaterialReference(handle.Id));
        }

        public RenderMaterialParameters ResolveMaterial(RendererBase renderer) => renderer.MaterialParameters;

        public void Dispose() => _host.Dispose();
    }

    /// <summary>材质测试数据（本文件 private）：带 BaseColor 默认色的资产，可选着色器依赖句柄</summary>
    private static class MaterialTestData
    {
        public static MaterialAsset CreateAssetWithColor(Vector3 color, AssetHandle<ShaderAsset>? shader = null)
            => new(
                new AssetId(Guid.NewGuid()),
                shader ?? new AssetHandle<ShaderAsset>(new AssetId(Guid.NewGuid())),
                null,
                new MaterialParameterSnapshot([("BaseColor", MaterialValue.Vector3(color))]),
                0);
    }
}