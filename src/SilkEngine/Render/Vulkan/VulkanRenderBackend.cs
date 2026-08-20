using System;
using System.Collections.Generic;
using Silk.NET.Windowing;

namespace SilkEngine.Render.Vulkan;

/// <summary>
/// Vulkan 渲染后端桩
/// <br/>仅负责窗口创建、上下文切换与一帧的绘制执行，线程调度由 ThreadManager 分配的专用执行者管理。
/// </summary>
public class VulkanRenderBackend : RenderBackendBase
{
    private IWindow? _window;

    /// <inheritdoc />
    public override bool ShouldClose => _window?.IsClosing ?? false;

    /// <inheritdoc />
    public override int Width => _window?.Size.X ?? 800;

    /// <inheritdoc />
    public override int Height => _window?.Size.Y ?? 600;

    /// <inheritdoc />
    public override void InitWindow()
    {
        _window = Silk.NET.Windowing.Window.Create(DefaultWindowOption.DefaultVulkanOption);
        _window.Initialize();
        // TODO:未完成的Vulkan
    }

    /// <inheritdoc />
    public override void MakeContextCurrent() => _window?.MakeCurrent();

    /// <inheritdoc />
    public override void ClearContext() => _window?.ClearContext();

    /// <inheritdoc />
    public override void PumpWindowEvents() => _window?.DoEvents();

    /// <inheritdoc />
    public override void ExecutePass(IReadOnlyList<DrawCommand> commands) { }

    /// <inheritdoc />
    public override void Present() { }

    /// <inheritdoc />
    public override IRenderBuffer CreateBuffer(int sizeBytes) =>
        throw new NotSupportedException($"[{GetType().Name}] CreateBuffer 未实现");

    /// <inheritdoc />
    public override void Dispose()
    {
        if (_disposed)
            return;

        _window?.Dispose();

        base.Dispose();
    }
}
