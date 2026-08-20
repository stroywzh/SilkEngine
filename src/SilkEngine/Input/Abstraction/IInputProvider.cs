using Silk.NET.Windowing;

namespace SilkEngine.InputSystem;

/// <summary>输入提供者抽象：宿主窗口输入接入点（SilkInputProvider 为 Silk.NET 实现）。</summary>
public interface IInputProvider : IDisposable
{
    /// <summary>绑定宿主窗口并创建输入上下文。</summary>
    /// <param name="window">宿主窗口（Silk.NET IWindow）</param>
    void Initialize(IWindow window);

    /// <summary>轮询并写入引擎双缓冲状态（帧首由 Input.Update 调用）。</summary>
    /// <param name="keyboard">键盘状态（写入当前缓冲）</param>
    /// <param name="mouse">鼠标状态（写入当前缓冲）</param>
    void UpdateInput(KeyboardState keyboard, MouseState mouse);
}
