using System;
using System.Collections.Generic;
using SilkEngine.Math;

namespace SilkEngine.InputSystem;

/// <summary>
/// 动作输入服务：持有命名 <see cref="InputActionMap"/>，每帧 <see cref="Update"/> 采样
/// 键盘/鼠标状态解析动作值；业务经 GetButton/GetButtonDown/GetButtonUp/GetAxis/GetMouseDelta 查询。
/// 仅依赖输入状态实例（生产由 EngineHost 装配到 <see cref="Input"/> 门面，业务不接触双缓冲细节）。
/// </summary>
public sealed class InputActionService
{
    private readonly KeyboardState _keyboard;
    private readonly MouseState _mouse;
    private readonly Dictionary<string, InputActionMap> _maps = new();
    private readonly Dictionary<(string Map, string Action), bool> _prevButtons = new();
    private readonly Dictionary<(string Map, string Action), bool> _currButtons = new();

    /// <summary>创建动作服务。</summary>
    /// <param name="keyboard">键盘状态源（帧首经 SwapBuffers + 提供者采样写入）</param>
    /// <param name="mouse">鼠标状态源（可为 null；无鼠标地图按零处理）</param>
    public InputActionService(KeyboardState keyboard, MouseState? mouse = null)
    {
        ArgumentNullException.ThrowIfNull(keyboard);
        _keyboard = keyboard;
        _mouse = mouse ?? new MouseState();
    }

    /// <summary>注册命名动作映射（重名抛 <see cref="InvalidOperationException"/>）。</summary>
    /// <param name="name">映射名</param>
    /// <param name="configure">动作声明回调</param>
    public void AddMap(string name, Action<InputActionMap> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);
        if (_maps.ContainsKey(name))
            throw new InvalidOperationException($"Action map '{name}' is already registered.");
        var map = new InputActionMap();
        configure(map);
        _maps[name] = map;
    }

    /// <summary>帧首采样：解析全部地图的按钮当前状态（沿判定基于上一帧）。</summary>
    public void Update()
    {
        _prevButtons.Clear();
        foreach (var (key, value) in _currButtons)
            _prevButtons[key] = value;
        _currButtons.Clear();
        foreach (var (mapName, map) in _maps)
            foreach (var (action, key) in map.Buttons)
                _currButtons[(mapName, action)] = _keyboard.GetKey(key);
    }

    /// <summary>按钮当前帧是否按住（地图/动作未配置恒 false）。</summary>
    public bool GetButton(string map, string action)
        => _currButtons.TryGetValue((map, action), out var held) && held;

    /// <summary>按钮本帧按下（上升沿；地图/动作未配置恒 false）。</summary>
    public bool GetButtonDown(string map, string action)
        => GetButton(map, action) && !_prevButtons.GetValueOrDefault((map, action));

    /// <summary>按钮本帧释放（下降沿；地图/动作未配置恒 false）。</summary>
    public bool GetButtonUp(string map, string action)
        => !GetButton(map, action) && _prevButtons.GetValueOrDefault((map, action));

    /// <summary>轴值（[-1, 1]：负向键 -1，正向键 +1，同按或均未按 0；未配置恒 0）。</summary>
    public float GetAxis(string map, string action)
    {
        if (!_maps.TryGetValue(map, out var m) || !m.Axes.TryGetValue(action, out var axis))
            return 0f;
        bool neg = _keyboard.GetKey(axis.Negative);
        bool pos = _keyboard.GetKey(axis.Positive);
        if (neg && pos)
            return 0f;
        if (neg)
            return -1f;
        if (pos)
            return 1f;
        return 0f;
    }

    /// <summary>鼠标增量值（本帧位移 × 灵敏度；未配置恒零）。</summary>
    public Vector2 GetMouseDelta(string map, string action)
    {
        if (!_maps.TryGetValue(map, out var m) || !m.MouseDeltas.TryGetValue(action, out var sensitivity))
            return Vector2.Zero;
        return _mouse.MoveVector * sensitivity;
    }
}
