using System.Collections.Generic;

namespace SilkEngine.Rendering.Abstraction;

/// <summary>
/// 一帧的不可变渲染交接（Main → Render 单向）：相机块、冻结渲染包与资源创建批次。
/// RenderThreadHost 帧内只读消费；禁止在 Render 域修改。
/// </summary>
/// <param name="Camera">相机帧值块（View/Projection 矩阵）</param>
/// <param name="Packets">冻结渲染包列表（帧内消费有效）</param>
/// <param name="Creates">本帧待创建的 GPU 资源批次</param>
public sealed record RenderSubmission(
    FrameCameraBlock Camera,
    IReadOnlyList<RenderPacket> Packets,
    RenderResourceCreateBatch Creates)
{
    /// <summary>空提交（无相机数据、无渲染包、无创建请求）。</summary>
    public static RenderSubmission Empty { get; } =
        new(FrameCameraBlock.Identity, [], RenderResourceCreateBatch.Empty);
}