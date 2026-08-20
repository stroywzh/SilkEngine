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

    /// <summary>将值钳制到 [0, 1] 区间。</summary>
    public static float Clamp01(float v) => MathF.Max(0f, MathF.Min(1f, v));

    /// <summary>线性插值；t 自动钳制到 [0, 1]（Unity 语义，t 越界时返回端点值）。</summary>
    public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

    public static float Abs(float v) => v < 0 ? -v : v;

    public static float Sign(float v) =>
        v > 0 ? 1f
        : v < 0 ? -1f
        : 0f;
}
