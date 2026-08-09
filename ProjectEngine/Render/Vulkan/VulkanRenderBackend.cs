using System;
using System.Threading;
using Silk.NET.Windowing;

namespace ProjectEngine.Render.Vulkan;

/// <summary>
/// Vulkan 渲染后端桩
/// <br/>继承 RenderBackendBase 共享线程管理，尚未实现实际 Vulkan 渲染。
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
    public override void Initialize(IntPtr windowHandle)
    {
        _window = Silk.NET.Windowing.Window.Create(DefaultWindowOption.DefaultVulkanOption);
        _window.Initialize();
        //TODO:未完成的Vulkan

        _renderThread = new Thread(RenderLoop) { Name = "VulkanRender", IsBackground = true };
        _rendering = true;
        _renderThread.Start();
    }

    private void RenderLoop()
    {
        while (_rendering)
        {
            _commandsReady.Wait();
            _commandsReady.Reset();
            if (!_rendering)
                break;
            ExecuteFrame();
            _frameDone.Set();
        }
    }

    /// <inheritdoc />
    public override void ProcessWindowEvents() => _window?.DoEvents();

    /// <inheritdoc />
    public override void ExecuteFrame()
    {
        _window!.SwapBuffers();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        if (_disposed)
            return;

        _window?.Dispose();

        base.Dispose();
    }
}
