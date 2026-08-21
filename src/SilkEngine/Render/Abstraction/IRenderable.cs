using SilkEngine.Math;

namespace SilkEngine.Render;

/// <summary>
/// 可渲染对象契约：渲染收集/管线消费的纯数据视图（Render 域定义，不依赖 Scene 类型）。
/// 当前实现为 Scene 域 MeshRenderer 组件；引擎内不存在反向场景引用。
/// </summary>
public interface IRenderable
{
    /// <summary>渲染着色器；null 时管线跳过该对象。</summary>
    Shader? Shader { get; }

    /// <summary>渲染网格；null 时管线跳过该对象。</summary>
    Mesh? Mesh { get; }

    /// <summary>渲染材质（uniform 参数容器）。</summary>
    Material? Material { get; }

    /// <summary>对象自身启用状态（批次收集已按此过滤，命令构建时保留语义）。</summary>
    bool Enabled { get; }

    /// <summary>对象世界变换矩阵（与 View/Projection 一并上传 uModel/uMVP）。</summary>
    Matrix4x4 WorldMatrix { get; }
}
