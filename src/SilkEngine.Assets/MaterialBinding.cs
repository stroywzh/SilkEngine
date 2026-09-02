using System.Collections.Generic;
using SilkEngine.Assets;
using SilkEngine.Assets.Binding;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Render;

/// <summary>
/// 材质绑定状态：<see cref="Ready"/> 可直接消费；<see cref="Stale"/> 载荷已刷新需重新上传；
/// <see cref="Loading"/> 资产或依赖未就绪；<see cref="Missing"/> 资产或依赖不存在；<see cref="Failed"/> 解析异常
/// </summary>
public enum MaterialBindingState
{
    /// <summary>绑定就绪，载荷可直接消费</summary>
    Ready,

    /// <summary>资产或依赖尚未加载，暂不可消费</summary>
    Loading,

    /// <summary>资产或依赖不存在，不可消费</summary>
    Missing,

    /// <summary>解析过程抛出异常，不可消费</summary>
    Failed,

    /// <summary>自上次发布后输入（覆盖参数/源修订/依赖修订）已变化，载荷为重新解析结果</summary>
    Stale,
}

/// <summary>材质绑定资产解析器：绑定层与资产系统之间的注入边界（实现方不得要求绑定层依赖 AssetManager）</summary>
public interface IMaterialAssetResolver
{
    /// <summary>解析材质资产</summary>
    /// <param name="id">资产 ID</param>
    /// <param name="isMissing">返回 null 时区分未加载（false）与不存在（true）</param>
    /// <returns>材质资产；未加载或不存在时为 null</returns>
    MaterialAsset? TryResolveMaterial(AssetId id, out bool isMissing);

    /// <summary>解析着色器资产（依赖门控）</summary>
    /// <param name="id">资产 ID</param>
    /// <param name="isMissing">返回 null 时区分未加载（false）与不存在（true）</param>
    /// <returns>着色器资产；未加载或不存在时为 null</returns>
    ShaderAsset? TryResolveShader(AssetId id, out bool isMissing);

    /// <summary>解析纹理资产（依赖门控）</summary>
    /// <param name="id">资产 ID</param>
    /// <param name="isMissing">返回 null 时区分未加载（false）与不存在（true）</param>
    /// <returns>纹理资产；未加载或不存在时为 null</returns>
    TextureAsset? TryResolveTexture(AssetId id, out bool isMissing);

    /// <summary>查询资产当前修订号（依赖变更检测）</summary>
    /// <param name="id">资产 ID</param>
    /// <returns>修订号；无修订信息时为 0</returns>
    ulong ResolveRevision(AssetId id);
}

/// <summary>材质绑定结果：状态 + 就绪载荷（仅 <see cref="Ready"/>/<see cref="Stale"/> 时 <see cref="Value"/> 非空）</summary>
public sealed class BoundMaterial
{
    /// <summary>绑定状态</summary>
    public MaterialBindingState State { get; }

    /// <summary>就绪载荷（State 为 Ready 或 Stale 时非空，否则为 null）</summary>
    public BoundMaterialValue? Value { get; }

    /// <summary>失败原因（State 为 Failed 时非空；含参数名与类型信息的诊断消息）</summary>
    public string? Error { get; }

    internal BoundMaterial(MaterialBindingState state, BoundMaterialValue? value, string? error = null)
    {
        State = state;
        Value = value;
        Error = error;
    }
}

/// <summary>绑定就绪载荷：合并后的只读渲染参数 + 依赖句柄 + 源/依赖修订号（发布后不可变）</summary>
public sealed class BoundMaterialValue
{
    /// <summary>合并后的无资产语义渲染参数快照（资产默认值被实例覆盖后；仅支持 Float/Vector3）</summary>
    public RenderMaterialParameters Parameters { get; }

    /// <summary>着色器依赖句柄</summary>
    public AssetHandle<ShaderAsset> Shader { get; }

    /// <summary>主纹理依赖句柄（可选）</summary>
    public AssetHandle<TextureAsset>? MainTexture { get; }

    /// <summary>源材质资产修订号（资产内容变更时递增）</summary>
    public ulong SourceRevision { get; }

    /// <summary>依赖资产修订号（着色器与纹理修订之和；0 表示无修订信息）</summary>
    public ulong DependencyRevision { get; }

    internal BoundMaterialValue(
        RenderMaterialParameters parameters,
        AssetHandle<ShaderAsset> shader,
        AssetHandle<TextureAsset>? mainTexture,
        ulong sourceRevision,
        ulong dependencyRevision)
    {
        Parameters = parameters;
        Shader = shader;
        MainTexture = mainTexture;
        SourceRevision = sourceRevision;
        DependencyRevision = dependencyRevision;
    }
}

/// <summary>
/// 材质绑定：将运行时材质实例解析为可消费的 <see cref="BoundMaterial"/>。
/// 内部保存已发布结果：输入（实例覆盖版本/源资产修订/依赖修订）与已发布结果一致时直接命中缓存；
/// 不一致时重新解析并发布为 <see cref="MaterialBindingState.Stale"/>。全程只读，绝不写回
/// <see cref="Material"/> 或 <see cref="MaterialAsset"/>。
/// </summary>
public sealed class MaterialBinding
{
    private readonly IMaterialAssetResolver _resolver;
    private readonly Dictionary<Material, PublishedBinding> _published = new(ReferenceEqualityComparer.Instance);

    /// <summary>创建材质绑定</summary>
    /// <param name="resolver">资产解析器（绑定层唯一的资产访问边界）</param>
    public MaterialBinding(IMaterialAssetResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        _resolver = resolver;
    }

    /// <summary>解析材质实例为绑定结果</summary>
    /// <param name="material">运行时材质实例</param>
    /// <returns>绑定结果：Ready 可直接消费；Stale 载荷已刷新需重新上传；Loading/Missing/Failed 不可消费</returns>
    public BoundMaterial Resolve(Material material)
    {
        ArgumentNullException.ThrowIfNull(material);

        try
        {
            if (_published.TryGetValue(material, out var published) && InputsMatch(material, published))
                return published.ReadyResult;

            return ResolveFresh(material, published is not null);
        }
        catch (Exception)
        {
            return new BoundMaterial(MaterialBindingState.Failed, null);
        }
    }

    private bool InputsMatch(Material material, PublishedBinding published)
    {
        if (material.Overrides.Version != published.OverridesVersion)
            return false;

        var asset = _resolver.TryResolveMaterial(material.Source.AssetId, out _);
        if (asset is null || asset.Revision != published.AssetRevision)
            return false;

        return ComputeDependencyRevision(asset) == published.DependencyRevision;
    }

    private BoundMaterial ResolveFresh(Material material, bool republish)
    {
        var asset = _resolver.TryResolveMaterial(material.Source.AssetId, out var isMissing);
        if (asset is null)
            return new BoundMaterial(isMissing ? MaterialBindingState.Missing : MaterialBindingState.Loading, null);

        var dependencyState = ResolveDependencies(asset);
        if (dependencyState != MaterialBindingState.Ready)
            return new BoundMaterial(dependencyState, null);

        if (TryValidateParameters(asset, material, out var validationError))
            return new BoundMaterial(MaterialBindingState.Failed, null, validationError);

        var parameters = MaterialResolver.ResolveForRender(material, asset.Defaults);
        var dependencyRevision = ComputeDependencyRevision(asset);
        var value = new BoundMaterialValue(parameters, asset.Shader, asset.MainTexture, asset.Revision, dependencyRevision);
        var readyResult = new BoundMaterial(MaterialBindingState.Ready, value);
        var result = republish ? new BoundMaterial(MaterialBindingState.Stale, value) : readyResult;

        _published[material] = new PublishedBinding(
            asset.Revision,
            material.Overrides.Version,
            dependencyRevision,
            value,
            readyResult);

        return result;
    }

    /// <summary>
    /// 参数类型校验：同名 defaults/overrides 类型不一致、或合并后存在渲染不支持的参数类型
    /// （非 Float/Vector3）时返回诊断错误消息；不写回任何输入。
    /// </summary>
    /// <param name="asset">源材质资产</param>
    /// <param name="material">运行时材质实例</param>
    /// <param name="error">校验失败时的错误消息（通过时为空）</param>
    /// <returns>true 表示校验失败</returns>
    private bool TryValidateParameters(MaterialAsset asset, Material material, out string? error)
    {
        var merged = new Dictionary<string, MaterialValue>();
        foreach (var (name, value) in asset.Defaults)
            merged[name] = value;
        foreach (var (name, value) in material.Overrides.Snapshot())
        {
            if (merged.TryGetValue(name, out var defaultValue) && defaultValue.Kind != value.Kind)
            {
                error = $"Material parameter '{name}' has conflicting value types: defaults={defaultValue.Kind}, overrides={value.Kind}";
                return true;
            }
            merged[name] = value;
        }
        foreach (var (name, value) in merged)
        {
            if (!MaterialResolver.IsConvertibleToRenderValue(value))
            {
                error = $"Material parameter '{name}' has unsupported type '{value.Kind}' for render (supported: Float, Vector3)";
                return true;
            }
        }
        error = null;
        return false;
    }

    private MaterialBindingState ResolveDependencies(MaterialAsset asset)
    {
        if (_resolver.TryResolveShader(asset.Shader.Id, out var shaderMissing) is null)
            return shaderMissing ? MaterialBindingState.Missing : MaterialBindingState.Loading;

        if (asset.MainTexture is { } texture &&
            _resolver.TryResolveTexture(texture.Id, out var textureMissing) is null)
            return textureMissing ? MaterialBindingState.Missing : MaterialBindingState.Loading;

        return MaterialBindingState.Ready;
    }

    private ulong ComputeDependencyRevision(MaterialAsset asset)
    {
        ulong revision = _resolver.ResolveRevision(asset.Shader.Id);
        if (asset.MainTexture is { } texture)
            revision += _resolver.ResolveRevision(texture.Id);
        return revision;
    }

    private sealed record PublishedBinding(
        ulong AssetRevision,
        int OverridesVersion,
        ulong DependencyRevision,
        BoundMaterialValue Payload,
        BoundMaterial ReadyResult);
}
