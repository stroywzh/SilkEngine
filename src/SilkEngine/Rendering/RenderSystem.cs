using System;
using System.Collections.Generic;
using SilkEngine.Core;
using SilkEngine.Render;
using SilkEngine.Rendering.Backend;
using SilkEngine.Threading;
using IRenderBackend = SilkEngine.Rendering.Backend.IRenderBackend;

namespace SilkEngine.Rendering;

/// <summary>
/// 渲染系统：主线程收集渲染数据、RenderThreadHost 专用渲染线程执行绘制，SubmitFrame 阻塞握手完成帧同步。
/// 后端与宿主均为 Rendering 契约（IRenderBackend/IManagedLoop），不解析任何资产类型。
/// </summary>
public sealed class RenderSystem : IDisposable
{
    private readonly IRenderBackend _backend;
    private readonly RenderThreadHost _renderThread;

    /// <summary>
    /// 创建渲染系统：装配 RenderThreadHost 并登记进 ThreadRuntime 受管循环与 Services。
    /// </summary>
    /// <param name="backend">渲染后端（窗口/上下文/绘制执行）</param>
    /// <param name="runtime">线程运行时（受管循环登记与关闭协议）</param>
    public RenderSystem(IRenderBackend backend, ThreadRuntime runtime)
    {
        _backend = backend;
        _renderThread = new RenderThreadHost(runtime, backend);
        runtime.RegisterManagedLoop(_renderThread);
        Services.Register(this);
    }

    /// <summary>渲染后端实例（Rendering 契约）</summary>
    public IRenderBackend Backend => _backend;

    /// <summary>窗口表面（后端实现 <see cref="IWindowSurface"/> 时提供；无窗口后端为 null）</summary>
    public IWindowSurface? Surface => _backend as IWindowSurface;

    /// <summary>渲染线程宿主（内部；release-request 队列排空器接线点）</summary>
    internal RenderThreadHost RenderHost => _renderThread;

    /// <summary>启动渲染线程（backend.Initialize 在渲染线程执行）。</summary>
    public void Initialize() => _renderThread.Start();

    /// <summary>处理窗口事件（主线程调用；无窗口后端 no-op）。</summary>
    public void PumpEvents() => Surface?.PumpWindowEvents();

    /// <summary>窗口是否已请求关闭（无窗口后端恒 false）。</summary>
    public bool ShouldClose => Surface?.ShouldClose ?? false;

    /// <summary>
    /// 主线程帧渲染入口：过渡期保留旧管线签名（相机矩阵更新 + 空帧提交驱动 Present）；
    /// RenderPacket 收集管线迁移后接入。
    /// </summary>
    /// <param name="aspect">视口宽高比（宽/高）</param>
    /// <param name="camera">当前相机视图（null 时跳过本帧渲染）</param>
    /// <param name="batches">渲染批次（过渡期参数，收集管线迁移后消费）</param>
    public void Render(float aspect, ICameraView? camera, IReadOnlyList<RenderBatch> batches)
    {
        if (camera == null)
            return;
        camera.UpdateMatrices(aspect);
        _renderThread.SubmitFrame([]);
    }

    /// <summary>释放渲染线程宿主（幂等；backend 由渲染线程 finally 释放）。</summary>
    public void Dispose() => _renderThread.Dispose();
}
