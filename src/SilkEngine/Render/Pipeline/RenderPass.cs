using System;

namespace SilkEngine.Render;

/// <summary>定义单个渲染 Pass，含可选筛选和前后 Hook。由 IRenderPipeline 按 SortOrder 升序执行。</summary>
public class RenderPass
{
    /// <summary>Pass 标识名称。</summary>
    public string Name { get; init; } = "";

    /// <summary>排序键，值越小越先执行。</summary>
    public int SortOrder { get; init; }

    /// <summary>可选筛选谓词。只有匹配的绘制命令会被渲染。</summary>
    public Func<DrawCommand, bool>? Filter { get; init; }

    /// <summary>在渲染命令之前调用的后端操作。</summary>
    public Action<IRenderBackend>? BeforeCommands { get; init; }

    /// <summary>在渲染命令之后调用的后端操作。</summary>
    public Action<IRenderBackend>? AfterCommands { get; init; }
}
