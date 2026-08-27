using SilkEngine.Assets;
using SilkEngine.Render;

namespace SilkEngine.Tests.Render;

public class MaterialBindingTests
{
    [Fact]
    public void Binding_MergesAssetDefaultsWithInstanceOverrides()
    {
        var materialId = new AssetId(Guid.NewGuid());
        var asset = Fixtures.MaterialAsset(
            materialId,
            defaults: [MaterialValueEntry.Float("Roughness", 0.4f)]);
        var resolver = new FakeAssetResolver(asset);
        var material = new Material(new MaterialReference(materialId));
        material.SetFloat("Roughness", 0.8f);

        var result = new MaterialBinding(resolver).Resolve(material);

        Assert.Equal(MaterialBindingState.Ready, result.State);
        Assert.Equal(0.8f, result.Value!.Parameters.GetFloat("Roughness"));
    }

    [Fact]
    public void Binding_ReturnsLoadingWithoutTouchingBusinessInstance()
    {
        var material = new Material(new MaterialReference(new AssetId(Guid.NewGuid())));
        var result = new MaterialBinding(new LoadingAssetResolver()).Resolve(material);

        Assert.Equal(MaterialBindingState.Loading, result.State);
        Assert.Empty(material.Overrides.Snapshot());
    }

    [Fact]
    public void Binding_Ready_CarriesHandlesAndRevisions()
    {
        var materialId = new AssetId(Guid.NewGuid());
        var shader = new AssetHandle<ShaderAsset>(new AssetId(Guid.NewGuid()));
        var texture = new AssetHandle<TextureAsset>(new AssetId(Guid.NewGuid()));
        var asset = Fixtures.MaterialAsset(
            materialId,
            defaults: [MaterialValueEntry.Float("Roughness", 0.4f)],
            shader: shader,
            mainTexture: texture,
            revision: 3);
        var resolver = new FakeAssetResolver(asset).SetDependencyRevision(shader.Id, 5);

        var result = new MaterialBinding(resolver).Resolve(new Material(new MaterialReference(materialId)));

        Assert.Equal(MaterialBindingState.Ready, result.State);
        Assert.Equal(shader, result.Value!.Shader);
        Assert.Equal(texture, result.Value.MainTexture);
        Assert.Equal(3UL, result.Value.SourceRevision);
        Assert.Equal(5UL, result.Value.DependencyRevision);
    }

    [Fact]
    public void Binding_ReturnsMissing_WhenAssetIsMissing()
    {
        var materialId = new AssetId(Guid.NewGuid());
        var resolver = new FakeAssetResolver().MarkMissing(materialId);

        var result = new MaterialBinding(resolver).Resolve(new Material(new MaterialReference(materialId)));

        Assert.Equal(MaterialBindingState.Missing, result.State);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Binding_ReturnsFailed_WhenResolverThrows()
    {
        var material = new Material(new MaterialReference(new AssetId(Guid.NewGuid())));
        var resolver = new FakeAssetResolver().ThrowOnResolve();

        var result = new MaterialBinding(resolver).Resolve(material);

        Assert.Equal(MaterialBindingState.Failed, result.State);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Binding_ReturnsLoading_WhenShaderDependencyNotLoaded()
    {
        var materialId = new AssetId(Guid.NewGuid());
        var asset = Fixtures.MaterialAsset(materialId, defaults: []);
        var resolver = new FakeAssetResolver(asset).RemoveShader(asset.Shader.Id);

        var result = new MaterialBinding(resolver).Resolve(new Material(new MaterialReference(materialId)));

        Assert.Equal(MaterialBindingState.Loading, result.State);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Binding_ReturnsMissing_WhenShaderDependencyMissing()
    {
        var materialId = new AssetId(Guid.NewGuid());
        var asset = Fixtures.MaterialAsset(materialId, defaults: []);
        var resolver = new FakeAssetResolver(asset).MarkMissing(asset.Shader.Id);

        var result = new MaterialBinding(resolver).Resolve(new Material(new MaterialReference(materialId)));

        Assert.Equal(MaterialBindingState.Missing, result.State);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Binding_ReturnsStale_WhenInstanceOverridesChange()
    {
        var materialId = new AssetId(Guid.NewGuid());
        var asset = Fixtures.MaterialAsset(materialId, defaults: [MaterialValueEntry.Float("Roughness", 0.4f)]);
        var binding = new MaterialBinding(new FakeAssetResolver(asset));
        var material = new Material(new MaterialReference(materialId));
        var first = binding.Resolve(material);

        material.SetFloat("Roughness", 0.9f);
        var second = binding.Resolve(material);

        Assert.Equal(MaterialBindingState.Ready, first.State);
        Assert.Equal(MaterialBindingState.Stale, second.State);
        Assert.Equal(0.9f, second.Value!.Parameters.GetFloat("Roughness"));
    }

    [Fact]
    public void Binding_ReturnsStale_WhenAssetRevisionChanges()
    {
        var materialId = new AssetId(Guid.NewGuid());
        var resolver = new FakeAssetResolver(Fixtures.MaterialAsset(materialId, defaults: [], revision: 1));
        var binding = new MaterialBinding(resolver);
        var material = new Material(new MaterialReference(materialId));
        var first = binding.Resolve(material);

        resolver.AddMaterial(Fixtures.MaterialAsset(materialId, defaults: [], revision: 2));
        var second = binding.Resolve(material);

        Assert.Equal(MaterialBindingState.Ready, first.State);
        Assert.Equal(MaterialBindingState.Stale, second.State);
        Assert.Equal(2UL, second.Value!.SourceRevision);
    }

    [Fact]
    public void Binding_ReturnsStale_WhenDependencyRevisionChanges()
    {
        var materialId = new AssetId(Guid.NewGuid());
        var asset = Fixtures.MaterialAsset(materialId, defaults: []);
        var resolver = new FakeAssetResolver(asset);
        var binding = new MaterialBinding(resolver);
        var material = new Material(new MaterialReference(materialId));
        var first = binding.Resolve(material);

        resolver.SetDependencyRevision(asset.Shader.Id, 7);
        var second = binding.Resolve(material);

        Assert.Equal(MaterialBindingState.Ready, first.State);
        Assert.Equal(MaterialBindingState.Stale, second.State);
        Assert.Equal(7UL, second.Value!.DependencyRevision);
    }

    [Fact]
    public void Binding_RepublishesReady_AfterStale()
    {
        var materialId = new AssetId(Guid.NewGuid());
        var asset = Fixtures.MaterialAsset(materialId, defaults: [MaterialValueEntry.Float("Roughness", 0.4f)]);
        var binding = new MaterialBinding(new FakeAssetResolver(asset));
        var material = new Material(new MaterialReference(materialId));
        binding.Resolve(material);

        material.SetFloat("Roughness", 0.9f);
        var stale = binding.Resolve(material);
        var third = binding.Resolve(material);

        Assert.Equal(MaterialBindingState.Stale, stale.State);
        Assert.Equal(MaterialBindingState.Ready, third.State);
        Assert.Same(stale.Value, third.Value);
    }

    [Fact]
    public void Binding_ReadyCacheHit_ReturnsSameInstance()
    {
        var materialId = new AssetId(Guid.NewGuid());
        var binding = new MaterialBinding(new FakeAssetResolver(Fixtures.MaterialAsset(materialId, defaults: [])));
        var material = new Material(new MaterialReference(materialId));

        var first = binding.Resolve(material);
        var second = binding.Resolve(material);

        Assert.Same(first, second);
        Assert.Equal(MaterialBindingState.Ready, second.State);
    }

    [Fact]
    public void MultipleInstances_ShareAsset_KeepIndependentParameters()
    {
        var materialId = new AssetId(Guid.NewGuid());
        var asset = Fixtures.MaterialAsset(materialId, defaults: [MaterialValueEntry.Float("Roughness", 0.4f)], revision: 2);
        var binding = new MaterialBinding(new FakeAssetResolver(asset));
        var first = new Material(new MaterialReference(materialId));
        var second = new Material(new MaterialReference(materialId));
        first.SetFloat("Roughness", 0.1f);
        second.SetFloat("Roughness", 0.9f);

        var firstResult = binding.Resolve(first);
        var secondResult = binding.Resolve(second);

        Assert.Equal(MaterialBindingState.Ready, firstResult.State);
        Assert.Equal(MaterialBindingState.Ready, secondResult.State);
        Assert.Equal(0.1f, firstResult.Value!.Parameters.GetFloat("Roughness"));
        Assert.Equal(0.9f, secondResult.Value!.Parameters.GetFloat("Roughness"));
        Assert.Equal(2UL, firstResult.Value.SourceRevision);
        Assert.Equal(2UL, secondResult.Value.SourceRevision);
    }

    [Fact]
    public void Binding_DoesNotWriteBackToAssetOrInstance()
    {
        var materialId = new AssetId(Guid.NewGuid());
        var asset = Fixtures.MaterialAsset(materialId, defaults: [MaterialValueEntry.Float("Roughness", 0.4f)]);
        var material = new Material(new MaterialReference(materialId));
        material.SetFloat("Roughness", 0.8f);

        new MaterialBinding(new FakeAssetResolver(asset)).Resolve(material);

        Assert.Equal(0.4f, asset.Defaults.GetFloat("Roughness"));
        Assert.Equal(1, material.Overrides.Count);
    }
}

/// <summary>绑定测试资产/解析器夹具（S5 渲染层绑定复用）</summary>
public static class Fixtures
{
    /// <summary>创建材质资产；未指定着色器/纹理句柄时自动生成</summary>
    /// <param name="id">材质资产 ID</param>
    /// <param name="defaults">默认参数集合</param>
    /// <param name="shader">着色器句柄（缺省自动生成）</param>
    /// <param name="mainTexture">主纹理句柄（可为 null）</param>
    /// <param name="revision">源资产修订号</param>
    /// <returns>材质资产</returns>
    public static MaterialAsset MaterialAsset(
        AssetId id,
        IEnumerable<(string Name, MaterialValue Value)> defaults,
        AssetHandle<ShaderAsset>? shader = null,
        AssetHandle<TextureAsset>? mainTexture = null,
        ulong revision = 0)
    {
        var shaderHandle = shader ?? new AssetHandle<ShaderAsset>(new AssetId(Guid.NewGuid()));
        return new MaterialAsset(id, shaderHandle, mainTexture, new MaterialParameterSnapshot(defaults), revision);
    }
}

/// <summary>参数条目工厂：生成 (名称, 值) 元组集合元素</summary>
public static class MaterialValueEntry
{
    /// <summary>创建浮点参数条目</summary>
    /// <param name="name">参数名称</param>
    /// <param name="value">浮点值</param>
    /// <returns>参数条目</returns>
    public static (string Name, MaterialValue Value) Float(string name, float value) => (name, MaterialValue.Float(value));
}

/// <summary>内存资产解析器：登记资产自动补充依赖；支持模拟 Missing/Loading/Failed 与依赖修订变更</summary>
public sealed class FakeAssetResolver : IMaterialAssetResolver
{
    private readonly Dictionary<AssetId, MaterialAsset> _materials = [];
    private readonly Dictionary<AssetId, ShaderAsset> _shaders = [];
    private readonly Dictionary<AssetId, TextureAsset> _textures = [];
    private readonly Dictionary<AssetId, ulong> _revisions = [];
    private readonly HashSet<AssetId> _missing = [];
    private bool _throwOnResolve;

    /// <summary>创建解析器并登记材质资产（依赖句柄对应资产自动补充）</summary>
    /// <param name="assets">材质资产集合</param>
    public FakeAssetResolver(params MaterialAsset[] assets)
    {
        foreach (var asset in assets)
            AddMaterial(asset);
    }

    /// <summary>登记或替换材质资产；依赖句柄对应资产缺失时自动补充</summary>
    /// <param name="asset">材质资产</param>
    /// <returns>本解析器（链式）</returns>
    public FakeAssetResolver AddMaterial(MaterialAsset asset)
    {
        _materials[asset.Id] = asset;
        _shaders.TryAdd(asset.Shader.Id, new ShaderAsset($"shader-{asset.Shader.Id.Value}", "void main(){}", "void main(){}"));
        if (asset.MainTexture is { } texture)
            _textures.TryAdd(texture.Id, new TextureAsset($"texture-{texture.Id.Value}", new ImageData(1, 1, [255, 255, 255, 255])));
        return this;
    }

    /// <summary>移除着色器资产（模拟依赖未加载）</summary>
    /// <param name="id">着色器资产 ID</param>
    /// <returns>本解析器（链式）</returns>
    public FakeAssetResolver RemoveShader(AssetId id)
    {
        _shaders.Remove(id);
        return this;
    }

    /// <summary>标记资产不存在（模拟 Missing）</summary>
    /// <param name="id">资产 ID</param>
    /// <returns>本解析器（链式）</returns>
    public FakeAssetResolver MarkMissing(AssetId id)
    {
        _missing.Add(id);
        return this;
    }

    /// <summary>设置依赖资产修订号（模拟依赖变更）</summary>
    /// <param name="id">依赖资产 ID</param>
    /// <param name="revision">修订号</param>
    /// <returns>本解析器（链式）</returns>
    public FakeAssetResolver SetDependencyRevision(AssetId id, ulong revision)
    {
        _revisions[id] = revision;
        return this;
    }

    /// <summary>开启解析抛异常（模拟 Failed）</summary>
    /// <returns>本解析器（链式）</returns>
    public FakeAssetResolver ThrowOnResolve()
    {
        _throwOnResolve = true;
        return this;
    }

    public MaterialAsset? TryResolveMaterial(AssetId id, out bool isMissing)
    {
        ThrowIfRequested();
        isMissing = _missing.Contains(id);
        return isMissing ? null : _materials.GetValueOrDefault(id);
    }

    public ShaderAsset? TryResolveShader(AssetId id, out bool isMissing)
    {
        ThrowIfRequested();
        isMissing = _missing.Contains(id);
        return isMissing ? null : _shaders.GetValueOrDefault(id);
    }

    public TextureAsset? TryResolveTexture(AssetId id, out bool isMissing)
    {
        ThrowIfRequested();
        isMissing = _missing.Contains(id);
        return isMissing ? null : _textures.GetValueOrDefault(id);
    }

    public ulong ResolveRevision(AssetId id) => _revisions.GetValueOrDefault(id);

    private void ThrowIfRequested()
    {
        if (_throwOnResolve)
            throw new InvalidOperationException("simulated resolver failure");
    }
}

/// <summary>永远返回未加载的解析器（模拟 Loading）</summary>
public sealed class LoadingAssetResolver : IMaterialAssetResolver
{
    public MaterialAsset? TryResolveMaterial(AssetId id, out bool isMissing)
    {
        isMissing = false;
        return null;
    }

    public ShaderAsset? TryResolveShader(AssetId id, out bool isMissing)
    {
        isMissing = false;
        return null;
    }

    public TextureAsset? TryResolveTexture(AssetId id, out bool isMissing)
    {
        isMissing = false;
        return null;
    }

    public ulong ResolveRevision(AssetId id) => 0;
}
