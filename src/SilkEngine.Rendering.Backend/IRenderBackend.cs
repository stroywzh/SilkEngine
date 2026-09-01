using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.Backend;

/// <summary>
/// 渲染后端帧执行能力：初始化、执行渲染包、提交画面，以及资源创建/释放（<see cref="IRenderDevice"/>）。
/// </summary>
/// <remarks>仅引用 <see cref="SilkEngine.Rendering.Abstraction"/> 与 BCL，不包含 OpenGL/Vulkan 类型。</remarks>
public interface IRenderBackend : IRenderDevice, IDisposable
{
    /// <summary>初始化后端（渲染线程启动阶段调用一次）。</summary>
    void Initialize();

    /// <summary>提交一个渲染包至当前帧。</summary>
    /// <param name="packet">不可变渲染提交数据。</param>
    void Execute(RenderPacket packet);

    /// <summary>交换前后缓冲，提交本帧画面。</summary>
    void Present();
}
