using Silk.NET.Windowing;

namespace ProjectEngine.Input;

public interface IInputProvider : IDisposable
{
    void Initialize(IWindow window);
    void UpdateInput(KeyboardState keyboard, MouseState mouse);
}
