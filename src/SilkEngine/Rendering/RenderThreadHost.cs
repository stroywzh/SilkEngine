using System;
using System.Collections.Generic;
using System.Threading;
using SilkEngine.Core;
using SilkEngine.Rendering.Abstraction;
using SilkEngine.Rendering.Backend;
using SilkEngine.Threading;

namespace SilkEngine.Rendering;

/// <summary>
/// 渲染线程宿主：Rendering 域的专用渲染循环（internal <see cref="IManagedLoop"/> 协议）。
/// 线程经 ThreadFactory 创建并自持（Join 不设固定安全超时）；
/// backend 生命周期归本类——Initialize 在渲染线程启动时调用，Dispose 在渲染线程退出 finally 中执行。
/// ThreadRuntime 仅按 IManagedLoop 协议统一 RequestStop + Join，不感知 Rendering 类型。
/// </summary>
internal sealed class RenderThreadHost : IManagedLoop, IDisposable
{
    private readonly ThreadRuntime _runtime;
    private readonly IRenderBackend _backend;
    private readonly ManualResetEventSlim _frameReady = new(false);
    private readonly ManualResetEventSlim _frameDone = new(false);
    private IReadOnlyList<RenderPacket> _pending = [];
    private Thread? _thread;
    private volatile bool _running;
    private int _disposed;

    /// <summary>
    /// 帧首释放请求排空器（Assets 侧主线程接线：AssetManager.ProcessUnloadQueue 形态）；
    /// 渲染线程每帧帧首调用，把排空的 release-request 队列逐条交给 backend.Release，不得丢弃。
    /// </summary>
    internal Action<Action<RenderResourceReleaseRequest>>? DrainUnloadQueue { get; set; }

    /// <summary>创建渲染线程宿主；backend 仅由渲染线程释放（本类不触碰其生命周期）。</summary>
    /// <param name="runtime">线程运行时（Render 域登记与关闭协议）</param>
    /// <param name="backend">渲染后端（Rendering 契约，无资产语义）</param>
    public RenderThreadHost(ThreadRuntime runtime, IRenderBackend backend)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    /// <summary>启动渲染线程（backend.Initialize 在渲染线程执行；重复启动抛 <see cref="InvalidOperationException"/>）。</summary>
    public void Start()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(RenderThreadHost));
        if (_running)
            throw new InvalidOperationException("RenderThreadHost 已启动");
        _running = true;
        _thread = ThreadFactory.CreateThread(Run, "RenderThread");
        _thread.Start();
    }

    /// <summary>提交一帧冻结渲染数据（主线程构建的不可变 RenderPacket 列表）并阻塞等待渲染线程完成（帧同步握手）。</summary>
    /// <param name="packets">冻结帧（帧内消费有效）</param>
    /// <exception cref="InvalidOperationException">宿主未运行或已释放</exception>
    public void SubmitFrame(IReadOnlyList<RenderPacket> packets)
    {
        if (!_running)
            throw new InvalidOperationException("RenderThreadHost 未运行");
        _pending = packets;
        _frameReady.Set();
        _frameDone.Wait();
        _frameDone.Reset();
    }

    /// <summary>渲染线程（启动后非 null；日志与线程元数据用途）。</summary>
    internal Thread? Thread => _thread;

    /// <summary>请求停止并唤醒阻塞的帧等待（幂等；线程退出归 Join）。</summary>
    public void RequestStop()
    {
        if (Volatile.Read(ref _disposed) != 0)
            return;
        _running = false;
        _frameReady.Set();
    }

    /// <summary>等待渲染线程退出（无固定安全超时；backend.Dispose 已随线程 finally 执行）。</summary>
    public void Join() => _thread?.Join();

    /// <summary>停止并回收线程（幂等；backend 仅由渲染线程 finally 释放一次）。</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _running = false;
        _frameReady.Set(); // 直接唤醒：RequestStop 的外部调用（ThreadRuntime）在已释放后为 no-op
        Join();
        _frameReady.Dispose();
        _frameDone.Dispose();
    }

    private void Run()
    {
        using (_runtime.EnterRender())
        {
            try
            {
                _backend.Initialize();
                while (_running)
                {
                    _frameReady.Wait();
                    _frameReady.Reset();
                    if (!_running)
                        break;
                    try
                    {
                        DrainUnloadQueue?.Invoke(_backend.Release);
                        foreach (var packet in _pending)
                            _backend.Execute(packet);
                        _backend.Present();
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[RenderThread] frame execution failed: {ex}");
                    }
                    finally
                    {
                        _frameDone.Set(); // 异常路径也必须放行主线程帧同步
                    }
                }
            }
            finally
            {
                _frameDone.Set(); // 停止路径放行可能阻塞中的 SubmitFrame
                _backend.Dispose(); // 渲染线程 finally：GPU 资源与 backend 退出释放
            }
        }
    }
}
