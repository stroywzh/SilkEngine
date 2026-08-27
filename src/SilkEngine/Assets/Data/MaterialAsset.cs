using SilkEngine.Assets;

namespace SilkEngine.Render;

/// <summary>材质资产：不可变定义（默认参数 + 着色器/纹理依赖句柄 + 源修订号），供绑定层解析为运行时绑定</summary>
public sealed class MaterialAsset
{
    /// <summary>材质资产唯一标识</summary>
    public AssetId Id { get; }

    /// <summary>着色器依赖句柄</summary>
    public AssetHandle<ShaderAsset> Shader { get; }

    /// <summary>主纹理依赖句柄（可选）</summary>
    public AssetHandle<TextureAsset>? MainTexture { get; }

    /// <summary>默认参数只读快照（构造后不可变）</summary>
    public MaterialParameterSnapshot Defaults { get; }

    /// <summary>源资产修订号（资产内容变更时递增，供绑定层判定 Stale）</summary>
    public ulong Revision { get; }

    /// <summary>创建材质资产</summary>
    /// <param name="id">材质资产唯一标识</param>
    /// <param name="shader">着色器依赖句柄</param>
    /// <param name="mainTexture">主纹理依赖句柄（可为 null）</param>
    /// <param name="defaults">默认参数快照</param>
    /// <param name="revision">源资产修订号（默认 0）</param>
    public MaterialAsset(
        AssetId id,
        AssetHandle<ShaderAsset> shader,
        AssetHandle<TextureAsset>? mainTexture,
        MaterialParameterSnapshot defaults,
        ulong revision = 0)
    {
        Id = id;
        Shader = shader;
        MainTexture = mainTexture;
        Defaults = defaults;
        Revision = revision;
    }

    /// <summary>
    /// 派生运行时材质实例：每次调用生成独立实例（覆盖参数集独立，互不影响）。
    /// 默认参数由绑定层在解析时合并，不写入实例覆盖。
    /// </summary>
    /// <returns>独立的运行时材质实例</returns>
    public Material ToInstance() => new(new MaterialReference(Id));
}
