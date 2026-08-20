using System.Collections.Generic;

namespace SilkEngine.InputSystem;

/// <summary>
/// 键盘双缓冲状态：帧首 SwapBuffers 清空当前缓冲，提供者随后写入本帧按键；
/// 查询 API 基于 上一帧/当前帧 集合判定沿。
/// </summary>
public class KeyboardState
{
    private HashSet<KeyCode> _prev = new(),
        _curr = new();

    /// <summary>当前帧是否按住指定键。</summary>
    /// <param name="key">按键码</param>
    /// <returns>按住为 true</returns>
    public bool GetKey(KeyCode key) => _curr.Contains(key);

    /// <summary>本帧是否按下指定键（上升沿）。</summary>
    /// <param name="key">按键码</param>
    /// <returns>本帧按下为 true</returns>
    public bool GetKeyDown(KeyCode key) => !_prev.Contains(key) && _curr.Contains(key);

    /// <summary>本帧是否释放指定键（下降沿）。</summary>
    /// <param name="key">按键码</param>
    /// <returns>本帧释放为 true</returns>
    public bool GetKeyUp(KeyCode key) => _prev.Contains(key) && !_curr.Contains(key);

    /// <summary>当前帧是否有任意按键按下。</summary>
    internal bool AnyKey => _curr.Count > 0;

    /// <summary>帧首交换：当前帧 → 上一帧，并清空当前帧集合。</summary>
    public void SwapBuffers()
    {
        (_prev, _curr) = (_curr, _prev);
        _curr.Clear();
    }

    /// <summary>写入当前帧按键状态（提供者采样调用；pressed=false 忽略，释放由帧清空表达）。</summary>
    /// <param name="key">按键码</param>
    /// <param name="pressed">是否按下</param>
    public void SetKey(KeyCode key, bool pressed)
    {
        if (pressed)
        {
            _curr.Add(key);
        }
    }
}
