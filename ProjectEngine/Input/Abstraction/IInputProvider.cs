using Silk.NET.Windowing;

namespace ProjectEngine.InputSystem;

public interface IInputProvider : IDisposable
{
    void Initialize(IWindow window);
    void UpdateInput(KeyboardState keyboard, MouseState mouse);
}
