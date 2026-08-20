using System.Collections.Generic;
using SilkEngine.Scene;

namespace SilkEngine.Render;

/// <summary>
/// 渲染管线策略：将相机与渲染批次转换为按 SortOrder 升序执行的 RenderPass 列表
/// </summary>
public interface IRenderPipeline
{
    /// <summary>
    /// 构建本帧渲染 Pass 列表
    /// </summary>
    /// <param name="camera">当前相机（矩阵已由 RenderSystem 预计算）</param>
    /// <param name="batches">渲染批次</param>
    /// <returns>按 SortOrder 升序执行的 Pass 列表</returns>
    IReadOnlyList<RenderPass> Build(Camera camera, IReadOnlyList<RenderBatch> batches);
}
