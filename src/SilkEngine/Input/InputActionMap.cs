using System.Collections.Generic;

namespace SilkEngine.InputSystem;

/// <summary>
/// 可配置动作映射：声明按钮/轴/鼠标增量绑定（业务消费的输入声明，经 <see cref="InputActionService"/> 解析）。
/// </summary>
public sealed class InputActionMap
{
    /// <summary>按钮绑定：动作名 → 按键码。</summary>
    public IReadOnlyDictionary<string, KeyCode> Buttons => _buttons;

    /// <summary>轴绑定：动作名 → 负向/正向按键。</summary>
    public IReadOnlyDictionary<string, (KeyCode Negative, KeyCode Positive)> Axes => _axes;

    /// <summary>鼠标增量绑定：动作名 → 灵敏度（默认 1）。</summary>
    public IReadOnlyDictionary<string, float> MouseDeltas => _mouseDeltas;

    private readonly Dictionary<string, KeyCode> _buttons = new();
    private readonly Dictionary<string, (KeyCode Negative, KeyCode Positive)> _axes = new();
    private readonly Dictionary<string, float> _mouseDeltas = new();

    /// <summary>声明按钮动作（按住即 true；GetButtonDown/Up 由服务按帧解析上升/下降沿）。</summary>
    /// <param name="name">动作名</param>
    /// <param name="key">绑定按键</param>
    public void Button(string name, KeyCode key) => _buttons[name] = key;

    /// <summary>声明轴动作（负向键 → -1，正向键 → +1，同时按下或均未按 → 0）。</summary>
    /// <param name="name">动作名</param>
    /// <param name="negative">负向按键</param>
    /// <param name="positive">正向按键</param>
    public void Axis(string name, KeyCode negative, KeyCode positive) => _axes[name] = (negative, positive);

    /// <summary>声明鼠标增量动作（每帧鼠标位移 × 灵敏度）。</summary>
    /// <param name="name">动作名</param>
    /// <param name="sensitivity">灵敏度乘数（默认 1）</param>
    public void MouseDelta(string name, float sensitivity = 1f) => _mouseDeltas[name] = sensitivity;
}
