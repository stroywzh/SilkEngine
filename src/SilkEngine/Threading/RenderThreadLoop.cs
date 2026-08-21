using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SilkEngine.Core;
using SilkEngine.Core.Assets;
using SilkEngine.Render;

namespace SilkEngine.Threading;

/// <summary>
/// 渲染工作器：仅负责后端生命周期、帧同步握手与 Passes 执行；线程控制权归 ThreadManager
/// （Initialize 绑定 ILoopExecutor，本类不创建/持有/释放线程）。
/// 帧握手超时 5s（FrameTimeout 可注入）；Dispose 不触碰执行者（Stop/Join 唯一属主 ThreadManager.Shutdown）。
/// </summary>
public class RenderThreadLoop : ThreadLoopBase
{
    private readonly IRenderBackend _backend;
    private readonly ManualResetEventSlim _commandsReady = new(false);
    private readonly ManualResetEventSlim _frameDone = new(false);
    private IReadOnlyList<RenderPass>? _pendingPasses;
    private bool _contextBound;

    /// <summary>已绑定的执行者（Initialize 前为 ctor 注入的执行者）。</summary>
    public ILoopExecutor ThreadLoop => Executor;

    /// <summary>窗口是否请求关闭（透传后端）。</summary>
    public bool ShouldClose => _backend.ShouldClose;

    /// <summary>帧握手超时（内部可注入，测试缩短；默认 5s）</summary>
    internal TimeSpan FrameTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>创建渲染工作器（未绑定，需先 Initialize）。</summary>
    /// <param name="backend">渲染后端（窗口/上下文/Passes 执行）</param>
    /// <param name="executor">执行者（ThreadManager 申请获得；生命周期归 ThreadManager）</param>
    public RenderThreadLoop(IRenderBackend backend, ILoopExecutor executor)
        : base(executor)
    {
        _backend = backend;
    }

    /// <summary>绑定执行者并启动渲染循环（基类 Start 绑定 Frame，返回 false 退出）。</summary>
    public void Initialize()
    {
        _backend.InitWindow();
        Start();
    }

    /// <summary>泵送窗口事件（透传后端；Embedded 模式下主循环跳过）。</summary>
    public void PumpEvents() => _backend.PumpWindowEvents();

    /// <summary>提交一帧渲染命令（主线程按 SortOrder 排序，渲染线程直读零排序分配）并阻塞等待渲染线程完成（帧同步握手；超时抛 TimeoutException）。</summary>
    /// <param name="passes">本帧渲染 Pass 列表（按 SortOrder 执行）</param>
    /// <exception cref="TimeoutException">握手在 FrameTimeout（默认 5s）内未完成</exception>
    public void SubmitFrame(IReadOnlyList<RenderPass> passes)
    {
        _pendingPasses = passes.OrderBy(p => p.SortOrder).ToArray();
        _commandsReady.Set();
        if (!_frameDone.Wait(FrameTimeout))
            throw new TimeoutException($"[RenderThread] frame handshake timeout after {FrameTimeout}");
        _frameDone.Reset();
    }

    /// <summary>等待工作：业务命令信号 + 基类停止令牌（Stop 唤醒后基类退出检查放行）。</summary>
    protected override void WaitForWork()
        => WaitHandle.WaitAny(new[] { _commandsReady.WaitHandle, StopEvent });

    /// <summary>渲染循环单帧帧体（执行者线程调用）：上下文绑定 → 帧首卸载队列 → Passes → Present 放行。</summary>
    protected override bool Tick()
    {
        if (!_contextBound)
        {
            _backend.MakeContextCurrent();
            _contextBound = true;
        }
        _commandsReady.Reset();
        try
        {
            // 帧首：处理资产释放队列（GL 释放由后端接入；无注册管理器（测试）时跳过）
            if (Services.TryGet<AssetManager>(out var assetManager))
                assetManager.ProcessUnloadQueue(asset => _backend.ReleaseGpuResource(asset));
            if (_pendingPasses != null)
            {
                foreach (var pass in _pendingPasses) // 已按 SortOrder 升序（SubmitFrame 主线程排序）
                {
                    pass.BeforeCommands?.Invoke(_backend);
                    _backend.ExecutePass(pass.Commands);
                    pass.AfterCommands?.Invoke(_backend);
                }
            }
            if (LogConfig.Render)
                Log.Info($"[Render] Frame submitted (passes: {_pendingPasses?.Count ?? 0})");
        }
        catch (Exception ex)
        {
            Log.Error($"[RenderThread] ExecutePass failed: {ex}");
        }
        finally
        {
            // pass 异常也必须 Present（Present 自身 try-catch 保护，不阻断帧同步放行）
            if (_pendingPasses != null)
            {
                try
                {
                    _backend.Present();
                }
                catch (Exception ex)
                {
                    Log.Error($"[RenderThread] Present failed: {ex}");
                }
            }
            _frameDone.Set(); // 异常路径也必须放行主线程
        }
        return true;
    }

    /// <summary>
    /// 停止渲染并唤醒阻塞握手（幂等）；不触碰执行者——
    /// 线程退出与回收归 ThreadManager.Shutdown（唯一 Stop/Join 属主）。
    /// </summary>
    public override void Dispose()
    {
        base.Dispose();
        _commandsReady.Set(); // 唤醒阻塞帧
        _commandsReady.Dispose();
        _frameDone.Dispose();
        _backend.Dispose();
        // 不再调用 _executor.Stop()/_executor.Join()——停止唯一属主 ThreadManager.Shutdown
    }
}
