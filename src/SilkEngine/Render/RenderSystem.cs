using System;
using System.Collections.Generic;
using SilkEngine.Core;
using SilkEngine.Threading;

namespace SilkEngine.Render;

/// <summary>
/// 渲染系统：主线程收集渲染数据、专用渲染线程执行绘制，SubmitFrame 阻塞握手完成帧同步。
/// <br/>每帧流程：Render（主线程收集 + 构建 Pass）→ SubmitFrame（阻塞等渲染线程执行完毕）。
/// </summary>
public sealed class RenderSystem : IDisposable
{
    private readonly IRenderBackend _backend;
    private readonly ThreadManager _threadManager;
    private readonly RenderThreadLoop _renderThread;
    private IRenderPipeline _pipeline;

    /// <summary>
    /// 创建渲染系统：从 ThreadManager 申请专用渲染线程执行者并注册进 Services
    /// </summary>
    /// <param name="backend">渲染后端（窗口/上下文/绘制执行）</param>
    /// <param name="threadManager">线程调度器（专用渲染线程申请来源）</param>
    /// <param name="pipeline">渲染管线（缺省 ForwardPipeline）</param>
    public RenderSystem(
        IRenderBackend backend,
        ThreadManager threadManager,
        IRenderPipeline? pipeline = null
    )
    {
        _backend = backend;
        _threadManager = threadManager;

        var executor = _threadManager.Request<ILoopExecutor>(
            new ThreadRequest("RenderThread", ThreadKind.Dedicated)
        );
        _renderThread = new RenderThreadLoop(backend, executor);
        _pipeline = pipeline ?? new ForwardPipeline();

        Services.Register(this);
    }

    /// <summary>渲染后端实例</summary>
    public IRenderBackend Backend => _backend;

    /// <summary>窗口是否已请求关闭（渲染线程状态）</summary>
    public bool ShouldClose => _renderThread.ShouldClose;

    /// <summary>渲染专用线程上下文（线程元数据：名称/NativeThreadId；用于日志与线程亲和断言）</summary>
    public ThreadContext RenderThreadContext => _renderThread.ThreadLoop.Context;

    /// <summary>从 ThreadManager 申请专用线程执行者并启动渲染循环（本类不持有线程）。</summary>
    public void Initialize()
    {
        _renderThread.Initialize();
    }

    /// <summary>处理窗口事件（主线程调用，Input 泵依赖）</summary>
    public void PumpEvents() => _renderThread.PumpEvents();

    /// <summary>
    /// 主线程帧渲染入口：更新相机矩阵（View/Projection 随命令上传，不突变材质）
    /// → 构建 Pass → SubmitFrame 阻塞等渲染线程执行完毕。
    /// </summary>
    /// <param name="aspect">视口宽高比（宽/高）</param>
    /// <param name="camera">当前相机视图（null 时跳过本帧渲染）</param>
    /// <param name="batches">渲染批次（EngineLoop 经 RenderCollector 组装）</param>
    public void Render(float aspect, ICameraView? camera, IReadOnlyList<RenderBatch> batches)
    {
        if (camera == null)
            return;
        camera.UpdateMatrices(aspect);
        var passes = _pipeline.Build(camera, batches);
        _renderThread.SubmitFrame(passes);
    }

    /// <summary>释放渲染线程与后端（幂等）</summary>
    public void Dispose() => _renderThread.Dispose();
}
