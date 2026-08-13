using SilkEngine.Core.Assets;
using SilkEngine.Render;

namespace SilkEngine;

public class MeshRenderer : Component
{
    private Shader? _shader;
    private Mesh? _mesh;
    private Material? _material;

    public Shader? Shader
    {
        get => _shader;
        set => AssetManager.SetTracked(ref _shader, value);
    }

    public Mesh? Mesh
    {
        get => _mesh;
        set => AssetManager.SetTracked(ref _mesh, value);
    }

    public Material? Material
    {
        get => _material;
        set => AssetManager.SetTracked(ref _material, value);
    }

    /// <summary>组件销毁：归还全部资产引用（引用归零的托管资产由帧末卸载）</summary>
    public override void OnDestroy()
    {
        AssetManager.SetTracked(ref _shader, null);
        AssetManager.SetTracked(ref _mesh, null);
        AssetManager.SetTracked(ref _material, null);
    }
}
