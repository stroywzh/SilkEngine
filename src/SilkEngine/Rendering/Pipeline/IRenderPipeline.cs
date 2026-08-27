using System.Collections.Generic;
using SilkEngine.Rendering.Abstraction;
using CameraView = SilkEngine.Render.ICameraView;
using RenderPacket = SilkEngine.Rendering.Abstraction.RenderPacket;

namespace SilkEngine.Rendering.Pipeline;

/// <summary>
/// 渲染管线策略：将相机与渲染批次转换为无资产语义的 RenderPacket 列表。
/// 不解析资产、不查询资产管理器，只复制 Render 值。
/// </summary>
public interface IRenderPipeline
{
    /// <summary>
    /// 构建本帧渲染包列表
    /// </summary>
    /// <param name="camera">当前相机视图（矩阵已由 RenderSystem 预计算）</param>
    /// <param name="batches">渲染批次</param>
    /// <returns>无资产语义的渲染包列表（帧内消费有效）</returns>
    IReadOnlyList<RenderPacket> Build(CameraView camera, IReadOnlyList<RenderBatch> batches);
}
