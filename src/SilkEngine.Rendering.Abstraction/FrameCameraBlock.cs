using SilkEngine.Math;

namespace SilkEngine.Rendering.Abstraction;

/// <summary>相机帧值块：随渲染提交携带的视图/投影矩阵（后端按 uniform 上传，不突变共享材质）。</summary>
/// <param name="View">视图矩阵（世界 → 相机）</param>
/// <param name="Projection">投影矩阵（相机 → 裁剪）</param>
public readonly record struct FrameCameraBlock(Matrix4x4 View, Matrix4x4 Projection)
{
    /// <summary>恒等相机块（无相机回退/测试占位）。</summary>
    public static FrameCameraBlock Identity { get; } = new(Matrix4x4.Identity, Matrix4x4.Identity);
}