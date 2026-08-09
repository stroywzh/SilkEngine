using System;
using System.Collections.Generic;

namespace ProjectEngine.Render;

/// <summary>
/// 渲染管线
/// <br/>编排多个 Pass 的渲染,将 DrawCommand 列表提交给后端
/// </summary>
public interface IRenderPipeline : IDisposable
{
    /// <summary>
    /// 使用指定渲染后端初始化管线
    /// </summary>
    void Initialize(IRenderBackend backend);

    /// <summary>
    /// 添加一个渲染 Pass
    /// </summary>
    void AddPass(RenderPass pass);

    /// <summary>
    /// 渲染DrawCommands
    /// <br/>按 SortOrder 升序执行所有 Pass，提交绘制命令
    /// </summary>
    void Render(IReadOnlyList<DrawCommand> commands);
}
