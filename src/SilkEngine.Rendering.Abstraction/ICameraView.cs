using SilkEngine.Math;

namespace SilkEngine.Rendering.Abstraction;

/// <summary>
/// 相机视图契约：视图/投影矩阵及其更新入口（Rendering.Abstraction 契约层定义，不依赖 Scene 与资产类型）。
/// 当前实现为 Scene 域 Camera 组件；RenderSystem 经此接口完成矩阵更新与命令上传。
/// </summary>
public interface ICameraView
{
    /// <summary>世界空间视图矩阵（UpdateMatrices 计算）。</summary>
    Matrix4x4 ViewMatrix { get; }

    /// <summary>投影矩阵（UpdateMatrices 计算）。</summary>
    Matrix4x4 ProjectionMatrix { get; }

    /// <summary>按视口宽高比重算视图/投影矩阵（RenderSystem 帧首调用）。</summary>
    /// <param name="aspect">视口宽高比（宽/高）</param>
    void UpdateMatrices(float aspect);
}
