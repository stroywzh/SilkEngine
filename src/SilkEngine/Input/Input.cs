using SilkEngine.Core;
using SilkEngine.Math;

namespace SilkEngine.InputSystem;

/// <summary>
/// 全局输入门面：帧首 Update 采样（双缓冲交换 → 提供者轮询），查询 API 读当前帧状态；
/// 虚拟轴（GetAxis）按 sensitivity 逼近 / gravity 归零平滑推进。
/// </summary>
public static class Input
{
    internal static IInputProvider? _provider;
    private static readonly KeyboardState _keyboard = new();
    private static readonly MouseState _mouse = new();
    private static readonly Dictionary<string, float> _axes = new();
    private static readonly KeyCode[] _keyCodes = Enum.GetValues<KeyCode>();

    /// <summary>键盘双缓冲状态（GetKey/GetKeyDown/GetKeyUp）</summary>
    public static KeyboardState Keyboard => _keyboard;

    /// <summary>鼠标双缓冲状态（位置/按钮/滚轮）</summary>
    public static MouseState Mouse => _mouse;
    private static bool _enableLog = false;
    /// <summary>输入日志开关：启用后每帧输出按键按下/释放（DEBUG 下含鼠标日志）。</summary>
    public static bool EnableLog
    {
        get => _enableLog;
        set => _enableLog = value;
    }

#if DEBUG
    private static bool _mouseLog = false;

    /// <summary>鼠标日志开关（DEBUG）：仅 EnableLog 开启或显式置 true 时生效。</summary>
    internal static bool EnableMouseLog
    {
        get => _mouseLog;
        set
        {
            if (EnableLog || value == true)
            {
                _mouseLog = value;
            }
        }
    }
#endif

    /// <summary>替换输入提供者；旧提供者立即释放（IDisposable）。</summary>
    public static void SetProvider(IInputProvider provider)
    {
        _provider?.Dispose();
        _provider = provider;
    }

    /// <summary>当前帧是否按住指定键。</summary>
    /// <param name="key">按键码</param>
    /// <returns>按住为 true</returns>
    public static bool GetKey(KeyCode key) => _keyboard.GetKey(key);

    /// <summary>本帧是否按下指定键（上升沿）。</summary>
    /// <param name="key">按键码</param>
    /// <returns>本帧按下为 true</returns>
    public static bool GetKeyDown(KeyCode key) => _keyboard.GetKeyDown(key);

    /// <summary>本帧是否释放指定键（下降沿）。</summary>
    /// <param name="key">按键码</param>
    /// <returns>本帧释放为 true</returns>
    public static bool GetKeyUp(KeyCode key) => _keyboard.GetKeyUp(key);

    /// <summary>鼠标当前位置（窗口像素坐标）。</summary>
    internal static Vector2 MousePosition => _mouse.Position;

    /// <summary>查询虚拟轴值（[-1,1]；未定义轴返回 0）。</summary>
    /// <param name="name">轴名（DefaultAxisMapping 内置 Horizontal/Vertical）</param>
    /// <returns>当前轴值</returns>
    public static float GetAxis(string name)
    {
        _axes.TryGetValue(name, out float v);
        return v;
    }

    /// <summary>帧首更新：双缓冲交换 → 提供者采样 → 日志输出 → 虚拟轴推进（EngineLoop 每帧调用）。</summary>
    internal static void Update()
    {
        _keyboard.SwapBuffers();
        _mouse.SwapBuffers();
        _provider?.UpdateInput(_keyboard, _mouse);

        if (EnableLog)
        {
            foreach (KeyCode kc in _keyCodes)
            {
                if (kc == KeyCode.None)
                    continue;

                if (_keyboard.GetKeyDown(kc))
                {
                    Log.Debug($"[Input] KeyDown: {kc}");
                }
                else if (_keyboard.GetKeyUp(kc))
                {
                    Log.Debug($"[Input] KeyUp: {kc}");
                }
            }
#if DEBUG
            if (EnableMouseLog)
            {
                if (_mouse.ScrollDelta != 0)
                {
                    Log.Debug($"[Input] Scroll: {_mouse.ScrollDelta:F2}");
                }

                if (_mouse.MoveVector != Vector2.Zero)
                {
                    Log.Debug(
                        $"[Input] Mouse: {_mouse.Position}, delta=({_mouse.MoveVector.X:F0},{_mouse.MoveVector.Y:F0})"
                    );
                }
            }

#endif
        }

        for (int i = 0; i < DefaultAxisMapping.DefaultAxes.Length; i++)
        {
            var axis = DefaultAxisMapping.DefaultAxes[i];
            float target = 0;
            if (_keyboard.GetKey(axis.positive))
            {
                target += 1;
            }

            if (_keyboard.GetKey(axis.negative))
            {
                target -= 1;
            }

            float current = _axes.GetValueOrDefault(axis.name, 0);
            if (MathF.Abs(target) > 0.01f)
            {
                current = Mathf.Clamp(
                    current + target * axis.sensitivity * Time.DeltaTime,
                    -1f,
                    1f
                );
            }
            else
            {
                current =
                    MathF.Abs(current) < 0.01f
                        ? 0
                        : current - Mathf.Sign(current) * axis.gravity * Time.DeltaTime;
            }

            _axes[axis.name] = Mathf.Clamp(current, -1f, 1f);
        }
    }
}
