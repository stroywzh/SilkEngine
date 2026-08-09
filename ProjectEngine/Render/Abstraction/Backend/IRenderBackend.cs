using System;
using System.Collections.Generic;

namespace ProjectEngine.Render;

/// <summary>
/// 图形 API 后端抽象
/// <br/>内部管理专用渲染线程——ExecuteFrame 在渲染线程运行，SubmitCommands 和 WaitForFrame 由主线程调用
/// </summary>
public interface IRenderBackend : IDisposable
{
    /// <summary>
    /// 初始化后端
    /// <br/>windowHandle 传 0 则自行创建窗口
    /// </summary>
    void Initialize(IntPtr windowHandle);

    /// <summary>
    /// 主线程轮询窗口事件
    /// <br/>输入、缩放、关闭等
    /// </summary>
    void ProcessWindowEvents();

    /// <summary>
    /// 将绘制命令队列送入渲染线程
    /// <br/>已确保线程安全
    /// </summary>
    void SubmitCommands(IReadOnlyList<DrawCommand> commands);

    /// <summary>
    /// 阻塞调用线程直至渲染线程完成处理
    /// </summary>
    void WaitForFrame();

    /// <summary>
    /// 在渲染线程上执行一帧渲染
    /// </summary>
    void ExecuteFrame();

    /// <summary>
    /// 创建 GPU 可见缓冲区（用于未来间接绘制）
    /// <br/>返回句柄
    /// </summary>
    IntPtr CreateBuffer(int sizeBytes);

    /// <summary>
    /// 从 GPU 缓冲区执行间接绘制
    /// <br/>未来 GPU-Driven 路径
    /// </summary>
    void DrawIndirect(IntPtr buffer, int offset, int drawCount);

    /// <summary>
    /// 窗口是否已请求关闭
    /// </summary>
    bool ShouldClose { get; }

    /// <summary>
    /// 当前帧缓冲区宽(像素)
    /// </summary>
    int Width { get; }

    /// <summary>
    /// 当前帧缓冲区高(像素)
    /// </summary>
    int Height { get; }
}
