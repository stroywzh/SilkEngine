using SilkEngine.Core.Assets;
using SilkEngine.Render;
using SilkEngine.Scene.Serialization;

namespace SilkEngine.Scene;

/// <summary>网格渲染组件：资产引用字段由源生成器序列化（GUID 路径，经 AssetRefCodec 属性感知规则，键=属性名）。</summary>
[SerializableInternal]
public partial class MeshRenderer : Component
{
    private Shader? _shader;
    private Mesh? _mesh;
    private Material? _material;

    /// <summary>渲染着色器；setter 经 AssetManager.SetTrackedAmbient 维持引用计数闭环。</summary>
    public Shader? Shader
    {
        get => _shader;
        set => AssetManager.SetTrackedAmbient(ref _shader, value);   // P1 落盘形式为准（C1）
    }

    /// <summary>渲染网格；setter 经 AssetManager.SetTrackedAmbient 维持引用计数闭环。</summary>
    public Mesh? Mesh
    {
        get => _mesh;
        set => AssetManager.SetTrackedAmbient(ref _mesh, value);
    }

    /// <summary>渲染材质；setter 经 AssetManager.SetTrackedAmbient 维持引用计数闭环。</summary>
    public Material? Material
    {
        get => _material;
        set => AssetManager.SetTrackedAmbient(ref _material, value);
    }

    /// <summary>组件销毁：归还全部资产引用（引用归零的托管资产由帧末卸载）。</summary>
    public override void OnDestroy()
    {
        AssetManager.SetTrackedAmbient(ref _shader, null);
        AssetManager.SetTrackedAmbient(ref _mesh, null);
        AssetManager.SetTrackedAmbient(ref _material, null);
    }
}
