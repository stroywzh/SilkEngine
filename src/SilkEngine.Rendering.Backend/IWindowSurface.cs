using Silk.NET.Windowing;

namespace SilkEngine.Rendering.Backend;

/// <summary>
/// 渲染窗口表面契约：无窗口后端可不实现；主线程窗口事件泵、尺寸与关闭状态经此访问。
/// 与绘制契约（<see cref="IRenderBackend"/>）分离，Rendering 域只消费接口，不解析具体窗口类型。
/// </summary>
public interface IWindowSurface
{
    /// <summary>原生窗口对象（供 Input 等子系统绑定事件源），无窗口时返回 null</summary>
    IWindow? NativeWindow { get; }

    /// <summary>窗口是否已请求关闭</summary>
    bool ShouldClose { get; }

    /// <summary>渲染表面宽度（像素）</summary>
    int Width { get; }

    /// <summary>渲染表面高度（像素）</summary>
    int Height { get; }

    /// <summary>处理窗口事件队列（主线程调用）</summary>
    void PumpWindowEvents();
}
