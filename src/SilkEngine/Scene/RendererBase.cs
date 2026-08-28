using SilkEngine.Assets;
using SilkEngine.Core;
using SilkEngine.Math;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Scene;

/// <summary>
/// 渲染组件基类：内部经 <see cref="AssetSlot{T}"/> 持有 Mesh/Shader 资产驻留（Assets/Scene 边界），
/// 对 Rendering collector 只暴露已解析的 Render Handle 与材质参数，不暴露任何资产载荷。
/// </summary>
public abstract class RendererBase : Component, IRenderable
{
    private AssetSlot<MeshAsset>? _meshSlot;
    private AssetSlot<ShaderAsset>? _shaderSlot;
    private RenderTextureHandle _textureHandle;
    private RenderMaterialParameters _materialParameters = new([]);

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

    /// <summary>已解析的网格 GPU 句柄（经资产管理器 GPU 句柄缓存；未发布或未驻留为 default）。</summary>
    public RenderMeshHandle MeshHandle => ResolveMesh();

    /// <summary>已解析的着色器 GPU 句柄（经资产管理器 GPU 句柄缓存；未发布或未驻留为 default）。</summary>
    public RenderShaderHandle ShaderHandle => ResolveShader();

    /// <summary>已解析的纹理 GPU 句柄（直接赋值；default 表示无纹理）。</summary>
    public RenderTextureHandle TextureHandle
    {
        get => _textureHandle;
        set => _textureHandle = value;
    }

    /// <summary>材质参数（渲染值集合；直接赋值，不参与资产驻留）。</summary>
    public RenderMaterialParameters MaterialParameters
    {
        get => _materialParameters;
        set => _materialParameters = value ?? new RenderMaterialParameters([]);
    }

    /// <summary>世界矩阵（对象世界变换，组合父级；IRenderable 契约适配）。</summary>
    public Matrix4x4 WorldMatrix => Transform.LocalToWorldMatrix;

    /// <summary>组件销毁：释放 Mesh/Shader 资产驻留槽（驻留归零的托管资产由帧末驱逐）。</summary>
    public override void OnDestroy()
    {
        _meshSlot?.Dispose();
        _shaderSlot?.Dispose();
        _meshSlot = null;
        _shaderSlot = null;
    }

    private RenderMeshHandle ResolveMesh()
    {
        if (Services.TryGet<AssetManager>(out var assets) && _meshSlot is { } slot
            && assets.TryGetRenderHandle(slot.Handle.Id, RenderResourceKind.Mesh, out var handle))
            return new RenderMeshHandle(handle);
        return default;
    }

    private RenderShaderHandle ResolveShader()
    {
        if (Services.TryGet<AssetManager>(out var assets) && _shaderSlot is { } slot
            && assets.TryGetRenderHandle(slot.Handle.Id, RenderResourceKind.Shader, out var handle))
            return new RenderShaderHandle(handle);
        return default;
    }
}
