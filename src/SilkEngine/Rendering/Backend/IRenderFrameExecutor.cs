using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.Backend;

/// <summary>
/// 整帧执行能力：后端一次性消费整份 <see cref="RenderSubmission"/>（相机块 + 渲染包 + 创建批次结果已由宿主处理），
/// 帧级状态（清屏、相机矩阵上传）由后端内部管理；不含 Present（渲染线程宿主统一调用）。
/// </summary>
public interface IRenderFrameExecutor
{
    /// <summary>执行整帧渲染提交。</summary>
    /// <param name="submission">本帧不可变提交</param>
    void ExecuteFrame(RenderSubmission submission);
}
