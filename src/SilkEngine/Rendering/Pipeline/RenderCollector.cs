using System.Collections.Generic;
using SilkEngine.Render;
using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.Pipeline;

/// <summary>一批渲染对象：当前实现为单批（全部活跃渲染器）。</summary>
public sealed class RenderBatch
{
    /// <summary>本批可渲染对象列表（实例被 RenderCollector 帧间复用，仅帧内消费有效）</summary>
    public IReadOnlyList<IRenderable> Renderers { get; set; } = [];
}

/// <summary>
/// 主线程渲染收集器：从已过滤的接口列表组装渲染批次（纯组装，无场景依赖）。
/// 活跃性过滤与默认相机回退由 EngineLoop（Scene 域查询）在上游完成。
/// 输出缓冲帧间复用（热路径零分配）：返回的批次列表为内部引用，消费须在本帧内完成
/// （帧序保证：RenderSystem.Render → SubmitFrame 阻塞等渲染线程执行完毕后才进入下一帧 Gather）。
/// </summary>
public sealed class RenderCollector
{
    private readonly List<RenderBatch> _batches = [];
    private readonly RenderBatch _batch = new();

    /// <summary>
    /// 收集当前帧渲染数据：相机取列表首个（过滤已在上游），渲染器非空时组装为单批。
    /// </summary>
    /// <param name="cameras">已过滤的相机视图列表（首个即当前相机；空列表时 camera 为 null）</param>
    /// <param name="renderables">已过滤的可渲染对象列表（仅含 Enabled 且层级活跃项；按引用挂入批次，不复制）</param>
    /// <param name="camera">选中的相机视图（列表首个；列表为空时为 null）</param>
    /// <param name="batches">组装出的渲染批次列表（无渲染器时为空；内部复用缓冲，仅帧内有效）</param>
    public void Gather(
        IReadOnlyList<ICameraView> cameras,
        IReadOnlyList<IRenderable> renderables,
        out ICameraView? camera,
        out IReadOnlyList<RenderBatch> batches
    )
    {
        camera = cameras.Count > 0 ? cameras[0] : null;
        _batches.Clear();
        if (renderables.Count > 0)
        {
            _batch.Renderers = renderables;
            _batches.Add(_batch);
        }
        batches = _batches;
    }
}
