using SilkEngine.Assets;
using SilkEngine.Assets.Binding;
using SilkEngine.Core;
using SilkEngine.Math;
using SilkEngine.Render;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Scene;

/// <summary>
/// 渲染组件基类：内部经 <see cref="AssetSlot{T}"/> 持有 Mesh/Shader/Texture 资产驻留（Assets/Scene 边界），
/// Material 经 <see cref="MaterialResolver"/> 解析为无资产语义的渲染参数；
/// 对 Rendering collector 只暴露已解析的 Render Handle 与材质参数，不暴露任何资产载荷。
/// </summary>
public abstract class RendererBase : Component, IRenderable
{
    private AssetSlot<MeshAsset>? _meshSlot;
    private AssetSlot<ShaderAsset>? _shaderSlot;
    private AssetSlot<TextureAsset>? _textureSlot;
    private RenderTextureHandle _textureHandle;
    private RenderMaterialParameters _materialParameters = new([]);
    private Material? _material;
    private RenderMaterialParameters? _materialParamsCache;
    private int _materialParamsVersion = -1;

    /// <summary>绑定网格资产驻留槽（旧槽释放；无资产管理器时仅记录句柄，解析结果为 default）。</summary>
    /// <param name="handle">网格资产句柄</param>
    public void SetMesh(AssetHandle<MeshAsset> handle)
    {
        _meshSlot?.Dispose();
        _meshSlot = Services.TryGet<AssetManager>(out var assets) ? assets.CreateSlot(handle) : null;
    }

    /// <summary>绑定着色器资产驻留槽（旧槽释放；无资产管理器时仅记录句柄，解析结果为 default）。</summary>
    /// <param name="handle">着色器资产句柄</param>
    public void SetShader(AssetHandle<ShaderAsset> handle)
    {
        _shaderSlot?.Dispose();
        _shaderSlot = Services.TryGet<AssetManager>(out var assets) ? assets.CreateSlot(handle) : null;
    }

    /// <summary>绑定纹理资产驻留槽（旧槽释放；无资产管理器时仅记录句柄，解析结果为 default）。</summary>
    /// <param name="handle">纹理资产句柄</param>
    public void SetTexture(AssetHandle<TextureAsset> handle)
    {
        _textureSlot?.Dispose();
        _textureSlot = Services.TryGet<AssetManager>(out var assets) ? assets.CreateSlot(handle) : null;
    }

    /// <summary>网格资产句柄（业务属性；赋值经 AssetSlot 驻留，旧槽自动释放）。</summary>
    public AssetHandle<MeshAsset> Mesh
    {
        get => _meshSlot?.Handle ?? default;
        set => SetMesh(value);
    }

    /// <summary>着色器资产句柄（业务属性；赋值经 AssetSlot 驻留，旧槽自动释放）。</summary>
    public AssetHandle<ShaderAsset> Shader
    {
        get => _shaderSlot?.Handle ?? default;
        set => SetShader(value);
    }

    /// <summary>纹理资产句柄（业务属性；赋值经 AssetSlot 驻留，旧槽自动释放）。</summary>
    public AssetHandle<TextureAsset> Texture
    {
        get => _textureSlot?.Handle ?? default;
        set => SetTexture(value);
    }

    /// <summary>已解析的网格 GPU 句柄（经资产管理器 GPU 句柄缓存；未发布或未驻留为 default）。</summary>
    public RenderMeshHandle MeshHandle => ResolveMesh();

    /// <summary>已解析的着色器 GPU 句柄（经资产管理器 GPU 句柄缓存；未发布或未驻留为 default）。</summary>
    public RenderShaderHandle ShaderHandle => ResolveShader();

    /// <summary>已解析的纹理 GPU 句柄（纹理槽绑定且已发布时经句柄缓存解析；否则回退直接赋值句柄）。</summary>
    public RenderTextureHandle TextureHandle
    {
        get => ResolveTexture();
        set => _textureHandle = value;
    }

    /// <summary>
    /// 材质运行时实例（业务属性）：赋值后经 <see cref="MaterialResolver"/> 解析为渲染参数
    /// （覆盖参数变更按 Version 惰性重解析）。置 null 清除材质参数。
    /// </summary>
    public Material? Material
    {
        get => _material;
        set
        {
            _material = value;
            _materialParamsCache = null;
            _materialParamsVersion = -1;
            if (value is null)
                _materialParameters = new RenderMaterialParameters([]);
        }
    }

    /// <summary>材质参数（渲染值集合；无材质实例时直接赋值；有材质实例时经解析器惰性生成）。</summary>
    public RenderMaterialParameters MaterialParameters
    {
        get => _material is { } m ? ResolveMaterialParameters(m) : _materialParameters;
        set
        {
            _material = null;
            _materialParamsCache = null;
            _materialParamsVersion = -1;
            _materialParameters = value ?? new RenderMaterialParameters([]);
        }
    }

    /// <summary>世界矩阵（对象世界变换，组合父级；IRenderable 契约适配）。</summary>
    public Matrix4x4 WorldMatrix => Transform.LocalToWorldMatrix;

    /// <summary>组件销毁：释放 Mesh/Shader/Texture 资产驻留槽（驻留归零的托管资产由帧末驱逐）。</summary>
    public override void OnDestroy()
    {
        _meshSlot?.Dispose();
        _shaderSlot?.Dispose();
        _textureSlot?.Dispose();
        _meshSlot = null;
        _shaderSlot = null;
        _textureSlot = null;
    }

    private RenderMeshHandle ResolveMesh()
    {
        if (Services.TryGet<AssetManager>(out var assets) && _meshSlot is { Handle: var h } && h != default
            && assets.TryGetRenderHandle(h.Id, RenderResourceKind.Mesh, out var handle))
            return new RenderMeshHandle(handle);
        return default;
    }

    private RenderShaderHandle ResolveShader()
    {
        if (Services.TryGet<AssetManager>(out var assets) && _shaderSlot is { Handle: var h } && h != default
            && assets.TryGetRenderHandle(h.Id, RenderResourceKind.Shader, out var handle))
            return new RenderShaderHandle(handle);
        return default;
    }

    private RenderTextureHandle ResolveTexture()
    {
        if (_textureSlot is { Handle: var h } && h != default)
        {
            if (Services.TryGet<AssetManager>(out var assets)
                && assets.TryGetRenderHandle(h.Id, RenderResourceKind.Texture, out var handle))
                return new RenderTextureHandle(handle);
            return default;
        }
        return _textureHandle;
    }

    private RenderMaterialParameters ResolveMaterialParameters(Material material)
    {
        if (_materialParamsCache is { } cached && material.Overrides.Version == _materialParamsVersion)
            return cached;
        _materialParamsVersion = material.Overrides.Version;
        return _materialParamsCache = MaterialResolver.ResolveForRender(material);
    }
}
