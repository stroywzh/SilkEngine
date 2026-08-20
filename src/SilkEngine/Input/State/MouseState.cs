using System;
using SilkEngine.Math;

namespace SilkEngine.InputSystem;

/// <summary>
/// 鼠标双缓冲状态：帧首 SwapBuffers 后由提供者写入当前缓冲，查询 API 读当前帧；
/// 按钮索引越界（≥3）静默忽略（恒 false）。
/// </summary>
public class MouseState
{
    private Vector2 _pos,
        _prevPos;
    private readonly bool[] _prevBtns = new bool[3],
        _currBtns = new bool[3];

    /// <summary>当前帧鼠标位置（窗口像素坐标）。</summary>
    public Vector2 Position => _pos;

    /// <summary>本帧位移（当前帧位置 − 上一帧位置）。</summary>
    public Vector2 MoveVector => _pos - _prevPos;

    /// <summary>本帧滚轮增量（SwapBuffers 时归零）。</summary>
    public float ScrollDelta { get; internal set; }

    /// <summary>左键当前帧是否按下。</summary>
    public bool LeftButton => GetButton(0);

    /// <summary>右键当前帧是否按下。</summary>
    public bool RightButton => GetButton(1);

    /// <summary>中键当前帧是否按下。</summary>
    public bool MiddleButton => GetButton(2);

    /// <summary>查询指定按钮当前帧状态（btn ≥ 3 越界恒 false）。</summary>
    /// <param name="btn">按钮索引（0=左 1=右 2=中）</param>
    /// <returns>按住为 true</returns>
    public bool GetButton(int btn) => btn < 3 && _currBtns[btn];

    /// <summary>本帧按下沿（上一帧未按、本帧按下）；越界恒 false。</summary>
    /// <param name="btn">按钮索引（0=左 1=右 2=中）</param>
    /// <returns>本帧按下为 true</returns>
    public bool GetButtonDown(int btn) => btn < 3 && !_prevBtns[btn] && _currBtns[btn];

    /// <summary>本帧释放沿（上一帧按下、本帧未按）；越界恒 false。</summary>
    /// <param name="btn">按钮索引（0=左 1=右 2=中）</param>
    /// <returns>本帧释放为 true</returns>
    public bool GetButtonUp(int btn) => btn < 3 && _prevBtns[btn] && !_currBtns[btn];

    /// <summary>帧首交换：上一帧 ← 当前帧，并清零滚轮增量。</summary>
    public void SwapBuffers()
    {
        _prevPos = _pos;
        Array.Copy(_currBtns, _prevBtns, 3);
        ScrollDelta = 0;
    }

    /// <summary>写入当前帧鼠标位置（提供者采样调用）。</summary>
    /// <param name="p">位置（窗口像素坐标）</param>
    public void SetPosition(Vector2 p) => _pos = p;

    /// <summary>写入当前帧按钮状态（提供者采样调用；越界静默忽略）。</summary>
    /// <param name="btn">按钮索引（0=左 1=右 2=中）</param>
    /// <param name="pressed">是否按下</param>
    public void SetButton(int btn, bool pressed)
    {
        if (btn < 3)
        {
            _currBtns[btn] = pressed;
        }
    }
}
