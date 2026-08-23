using SilkEngine.Assets;
using SilkEngine.Math;
using SilkEngine.Render;

namespace SilkEngine.Scene;

/// <summary>资产承载渲染组件基类：Mesh + Material + Shader 三资产 + 引用计数闭环 + IRenderable 实现。</summary>
public abstract class RendererBase : Component, IRenderable
{
    private Shader? _shader;
    private Mesh? _mesh;
    private Material? _material;

    /// <summary>渲染着色器；setter 经 AssetManager.SetTrackedAmbient 维持引用计数闭环。</summary>
    public Shader? Shader
    {
        get => _shader;
        set => AssetManager.SetTrackedAmbient(ref _shader, value);
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

    /// <summary>世界矩阵（对象世界变换，组合父级；IRenderable 契约适配）。</summary>
    public Matrix4x4 WorldMatrix => Transform.LocalToWorldMatrix;

    /// <summary>组件销毁：归还全部资产引用（引用归零的托管资产由帧末卸载）。</summary>
    public override void OnDestroy()
    {
        AssetManager.SetTrackedAmbient(ref _shader, null);
        AssetManager.SetTrackedAmbient(ref _mesh, null);
        AssetManager.SetTrackedAmbient(ref _material, null);
    }
}
