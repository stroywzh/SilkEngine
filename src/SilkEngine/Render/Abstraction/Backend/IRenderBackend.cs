using System;
using System.Collections.Generic;
using SilkEngine.Core.Assets;

namespace SilkEngine.Render;

/// <summary>
/// 渲染后端抽象：窗口与图形上下文生命周期 + 帧执行 + GPU 资源句柄管理。
/// <br/>ExecutePass/Present/DrawIndirect 等绘制方法仅在渲染线程上下文内调用；
/// CreateBuffer 返回的句柄具生命周期（调用方负责 Dispose，幂等），释放后访问抛 ObjectDisposedException。
/// </summary>
public interface IRenderBackend : IDisposable
{
    /// <summary>创建并初始化原生窗口与图形 API（渲染线程启动阶段调用一次）</summary>
    void InitWindow();

    /// <summary>使当前线程成为上下文所有者（渲染线程帧内调用）</summary>
    void MakeContextCurrent();

    /// <summary>解除当前线程的上下文绑定</summary>
    void ClearContext();

    /// <summary>处理窗口事件队列</summary>
    void PumpWindowEvents();

    /// <summary>在渲染线程上下文内执行一批绘制命令</summary>
    void ExecutePass(IReadOnlyList<DrawCommand> commands);

    /// <summary>交换前后缓冲，提交本帧画面（渲染线程上下文内调用）</summary>
    void Present();

    /// <summary>
    /// 创建 GPU 缓冲句柄（渲染线程上下文内调用）。
    /// <br/>句柄具生命周期：调用方负责 Dispose（幂等）；释放后访问抛 ObjectDisposedException。
    /// </summary>
    /// <param name="sizeBytes">缓冲大小（字节）</param>
    /// <returns>GPU 缓冲句柄</returns>
    IRenderBuffer CreateBuffer(int sizeBytes);

    /// <summary>以 GPU 端参数执行间接绘制（渲染线程上下文内调用）</summary>
    /// <param name="buffer">含绘制参数的命令缓冲</param>
    /// <param name="offset">缓冲内参数起始偏移（字节）</param>
    /// <param name="drawCount">执行绘制次数</param>
    void DrawIndirect(IRenderBuffer buffer, int offset, int drawCount);

    /// <summary>窗口是否已请求关闭</summary>
    bool ShouldClose { get; }

    /// <summary>渲染表面宽度（像素）</summary>
    int Width { get; }

    /// <summary>渲染表面高度（像素）</summary>
    int Height { get; }

    /// <summary>原生窗口对象（供 Input 等子系统绑定事件源），无窗口时返回 null</summary>
    Silk.NET.Windowing.IWindow? NativeWindow { get; }

    /// <summary>释放指定纹理的 GL 资源（渲染线程，帧首卸载队列处理）</summary>
    void ReleaseTexture(Texture2D texture);

    /// <summary>释放指定资产的 GPU 资源（渲染线程，帧首卸载队列处理）</summary>
    void ReleaseGpuResource(IAsset asset);
}
