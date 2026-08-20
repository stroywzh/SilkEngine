namespace SilkEngine.Math;

public static class Mathf
{
    public const float Deg2Rad = MathF.PI / 180f;
    public const float Rad2Deg = 180f / MathF.PI;
    public const float Epsilon = 1e-5f;

    public static float Clamp(float value, float min, float max)
    {
        if (value < min)
            return min;

        if (value > max)
            return max;

        return value;
    }

    public static float Lerp(float a, float b, float t) => a + (b - a) * t;

    public static float Abs(float v) => v < 0 ? -v : v;

    public static float Sign(float v) =>
        v > 0 ? 1f
        : v < 0 ? -1f
        : 0f;
}
