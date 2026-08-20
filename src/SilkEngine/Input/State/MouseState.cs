using System;
using SilkEngine.Math;

namespace SilkEngine.InputSystem;

public class MouseState
{
    private Vector2 _pos,
        _prevPos;
    private readonly bool[] _prevBtns = new bool[3],
        _currBtns = new bool[3];

    public Vector2 Position => _pos;
    public Vector2 MoveVector => _pos - _prevPos;
    public float ScrollDelta { get; internal set; }

    public bool LeftButton => GetButton(0);
    public bool RightButton => GetButton(1);
    public bool MiddleButton => GetButton(2);

    public bool GetButton(int btn) => btn < 3 && _currBtns[btn];

    public bool GetButtonDown(int btn) => btn < 3 && !_prevBtns[btn] && _currBtns[btn];

    public bool GetButtonUp(int btn) => btn < 3 && _prevBtns[btn] && !_currBtns[btn];

    public void SwapBuffers()
    {
        _prevPos = _pos;
        Array.Copy(_currBtns, _prevBtns, 3);
        ScrollDelta = 0;
    }

    public void SetPosition(Vector2 p) => _pos = p;

    public void SetButton(int btn, bool pressed)
    {
        if (btn < 3)
        {
            _currBtns[btn] = pressed;
        }
    }
}
