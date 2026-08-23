using SilkEngine.Math;
using SilkEngine.Render;

namespace SilkEngine.Scene;

/// <summary>UI 渲染组件（空壳；RendererBase 抽取与装配见子计划 B）。</summary>
public class UIRenderer : Component, IRenderable
{
    public Shader? Shader => throw new NotImplementedException();

    public Mesh? Mesh => throw new NotImplementedException();

    public Material? Material => throw new NotImplementedException();

    public Matrix4x4 WorldMatrix => throw new NotImplementedException();
}
