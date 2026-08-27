using SilkEngine.Rendering.Abstraction;

namespace SilkEngine.Rendering.Backend;

/// <summary>渲染后端帧执行能力：初始化、执行渲染包、提交画面与释放 GPU 资源。</summary>
/// <remarks>仅引用 <see cref="SilkEngine.Rendering.Abstraction"/> 与 BCL，不包含 OpenGL/Vulkan 类型。</remarks>
public interface IRenderBackend : IDisposable
{
    /// <summary>初始化后端（渲染线程启动阶段调用一次）。</summary>
    void Initialize();

    /// <summary>提交一个渲染包至当前帧。</summary>
    /// <param name="packet">不可变渲染提交数据。</param>
    void Execute(RenderPacket packet);

    /// <summary>交换前后缓冲，提交本帧画面。</summary>
    void Present();

    /// <summary>释放 GPU 资源。</summary>
    /// <param name="request">资源释放请求（仅含种类与句柄数值）。</param>
    void Release(RenderResourceReleaseRequest request);
}
