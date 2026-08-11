using ProjectEngine;
using ProjectEngine.Math;

namespace ProjectEngine.Input;

public static class Input
{
    internal static IInputProvider? _provider;
    private static readonly KeyboardState _keyboard = new();
    private static readonly MouseState _mouse = new();
    private static readonly Dictionary<string, float> _axes = new();

    public static KeyboardState Keyboard => _keyboard;
    public static MouseState Mouse => _mouse;

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
        Time.DeltaTime = Time.DeltaTime > 0 ? Time.DeltaTime : 0.016f;
        _provider?.UpdateInput(_keyboard, _mouse);

        foreach (var axis in DefaultAxisMapping.DefaultAxes)
        {
            float target = 0;
            if (_keyboard.GetKey(axis.Positive)) target += 1;
            if (_keyboard.GetKey(axis.Negative)) target -= 1;

            float current = _axes.GetValueOrDefault(axis.Name, 0);
            if (MathF.Abs(target) > 0.01f)
                current = Math.Clamp(current + target * axis.Sensitivity * Time.DeltaTime, -1f, 1f);
            else
                current = MathF.Abs(current) < 0.01f ? 0
                    : current - MathF.Sign(current) * axis.Gravity * Time.DeltaTime;
            _axes[axis.Name] = Math.Clamp(current, -1f, 1f);
        }
    }

    private static class Math
    {
        public static float Clamp(float v, float min, float max) =>
            v < min ? min : v > max ? max : v;
    }
}
