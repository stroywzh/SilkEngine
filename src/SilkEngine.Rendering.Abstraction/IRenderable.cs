using SilkEngine.Math;

namespace SilkEngine.Rendering.Abstraction;

/// <summary>
/// 可渲染对象契约：渲染收集/管线消费的无资产语义数据视图（Rendering 域定义，不依赖 Scene 类型）。
/// 只暴露已解析的 Render Handle 与参数值，不暴露任何资产载荷或资产身份。
/// </summary>
public interface IRenderable
{
    /// <summary>已解析的着色器 GPU 句柄；default 时管线跳过该对象。</summary>
    RenderShaderHandle ShaderHandle { get; }

    /// <summary>已解析的网格 GPU 句柄；default 时管线跳过该对象。</summary>
    RenderMeshHandle MeshHandle { get; }

    /// <summary>已解析的纹理 GPU 句柄（default 表示无纹理）。</summary>
    RenderTextureHandle TextureHandle { get; }

    /// <summary>材质参数（渲染值集合，无资产语义）。</summary>
    RenderMaterialParameters MaterialParameters { get; }

    /// <summary>对象自身启用状态（批次收集已按此过滤，命令构建时保留语义）。</summary>
    bool Enabled { get; }

    /// <summary>对象世界变换矩阵（模型矩阵上传）。</summary>
    Matrix4x4 WorldMatrix { get; }
}
