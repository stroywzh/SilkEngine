using System;
using System.Linq;
using Silk.NET.Input;
using Silk.NET.Windowing;
using SilkEngine.Math;

namespace SilkEngine.InputSystem;

public class SilkInputProvider : IInputProvider
{
    private static readonly KeyCode[] _keyCodes = Enum.GetValues<KeyCode>();
    private IInputContext? _input;
    private IKeyboard? _keyboard;
    private IMouse? _mouse;

    public void Initialize(IWindow window)
    {
        _input = window.CreateInput();
        _keyboard = _input.Keyboards.FirstOrDefault();
        _mouse = _input.Mice.FirstOrDefault();
    }

    public void UpdateInput(KeyboardState kb, MouseState ms)
    {
        if (_keyboard != null)
        {
            foreach (KeyCode kc in _keyCodes)
            {
                if (kc == KeyCode.None)
                    continue;

                var sk = Map(kc);
                if (sk == Key.Unknown)
                    continue;

                kb.SetKey(kc, _keyboard.IsKeyPressed(sk));
            }
        }
        if (_mouse != null)
        {
            ms.SetPosition(new Vector2(_mouse.Position.X, _mouse.Position.Y));
            ms.ScrollDelta = (float)_mouse.ScrollWheels.FirstOrDefault().Y;
            ms.SetButton(0, _mouse.IsButtonPressed(MouseButton.Left));
            ms.SetButton(1, _mouse.IsButtonPressed(MouseButton.Right));
            ms.SetButton(2, _mouse.IsButtonPressed(MouseButton.Middle));
        }
    }

    private static Key Map(KeyCode kc) =>
        kc switch
        {
            KeyCode.A => Key.A,
            KeyCode.B => Key.B,
            KeyCode.C => Key.C,
            KeyCode.D => Key.D,
            KeyCode.E => Key.E,
            KeyCode.F => Key.F,
            KeyCode.G => Key.G,
            KeyCode.H => Key.H,
            KeyCode.I => Key.I,
            KeyCode.J => Key.J,
            KeyCode.K => Key.K,
            KeyCode.L => Key.L,
            KeyCode.M => Key.M,
            KeyCode.N => Key.N,
            KeyCode.O => Key.O,
            KeyCode.P => Key.P,
            KeyCode.Q => Key.Q,
            KeyCode.R => Key.R,
            KeyCode.S => Key.S,
            KeyCode.T => Key.T,
            KeyCode.U => Key.U,
            KeyCode.V => Key.V,
            KeyCode.W => Key.W,
            KeyCode.X => Key.X,
            KeyCode.Y => Key.Y,
            KeyCode.Z => Key.Z,
            KeyCode.D0 => Key.Number0,
            KeyCode.D1 => Key.Number1,
            KeyCode.D2 => Key.Number2,
            KeyCode.D3 => Key.Number3,
            KeyCode.D4 => Key.Number4,
            KeyCode.D5 => Key.Number5,
            KeyCode.D6 => Key.Number6,
            KeyCode.D7 => Key.Number7,
            KeyCode.D8 => Key.Number8,
            KeyCode.D9 => Key.Number9,
            KeyCode.Space => Key.Space,
            KeyCode.Enter => Key.Enter,
            KeyCode.Escape => Key.Escape,
            KeyCode.Tab => Key.Tab,
            KeyCode.Backspace => Key.Backspace,
            KeyCode.Delete => Key.Delete,
            KeyCode.LeftShift => Key.ShiftLeft,
            KeyCode.RightShift => Key.ShiftRight,
            KeyCode.LeftControl => Key.ControlLeft,
            KeyCode.RightControl => Key.ControlRight,
            KeyCode.LeftAlt => Key.AltLeft,
            KeyCode.RightAlt => Key.AltRight,
            KeyCode.LeftArrow => Key.Left,
            KeyCode.RightArrow => Key.Right,
            KeyCode.UpArrow => Key.Up,
            KeyCode.DownArrow => Key.Down,
            KeyCode.F1 => Key.F1,
            KeyCode.F2 => Key.F2,
            KeyCode.F3 => Key.F3,
            KeyCode.F4 => Key.F4,
            KeyCode.F5 => Key.F5,
            KeyCode.F6 => Key.F6,
            KeyCode.F7 => Key.F7,
            KeyCode.F8 => Key.F8,
            KeyCode.F9 => Key.F9,
            KeyCode.F10 => Key.F10,
            KeyCode.F11 => Key.F11,
            KeyCode.F12 => Key.F12,
            _ => Key.Unknown,
        };

    /// <summary>释放持有 IInputContext（含其子键盘/鼠标），不再更新输入状态。</summary>
    public void Dispose()
    {
        _input?.Dispose();
        _input = null;
        _keyboard = null;
        _mouse = null;
    }
}
