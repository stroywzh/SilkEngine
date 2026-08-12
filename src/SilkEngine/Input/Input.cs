using SilkEngine;
using SilkEngine.Math;

namespace SilkEngine.InputSystem;

public static class Input
{
    internal static IInputProvider? _provider;
    private static readonly KeyboardState _keyboard = new();
    private static readonly MouseState _mouse = new();
    private static readonly Dictionary<string, float> _axes = new();

    public static KeyboardState Keyboard => _keyboard;
    public static MouseState Mouse => _mouse;
    private static bool _enableLog = false;
    public static bool EnableLog
    {
        get => _enableLog;
        set => _enableLog = value;
    }

#if DEBUG
    private static bool _mouseLog = false;

    public static bool EnableMouseLog
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

    public static void SetProvider(IInputProvider provider) => _provider = provider;

    public static bool GetKey(KeyCode key) => _keyboard.GetKey(key);

    public static bool GetKeyDown(KeyCode key) => _keyboard.GetKeyDown(key);

    public static bool GetKeyUp(KeyCode key) => _keyboard.GetKeyUp(key);

    public static Vector2 MousePosition => _mouse.Position;

    public static float GetAxis(string name)
    {
        _axes.TryGetValue(name, out float v);
        return v;
    }

    internal static void Update()
    {
        _keyboard.SwapBuffers();
        _mouse.SwapBuffers();
        _provider?.UpdateInput(_keyboard, _mouse);

        if (EnableLog)
        {
            foreach (KeyCode kc in Enum.GetValues<KeyCode>())
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
