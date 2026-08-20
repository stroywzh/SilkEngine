namespace SilkEngine.Math;

/// <summary>
/// 数学工具静态类（纯标量运算，不涉及坐标系；
/// 引擎向量/矩阵为左手系、行主序存储、GL 上传 transpose=true，本类工具与这些约定无关）。
/// </summary>
public static class Mathf
{
    /// <summary>度转弧度系数：π / 180。</summary>
    public const float Deg2Rad = MathF.PI / 180f;

    /// <summary>弧度转度系数：180 / π。</summary>
    public const float Rad2Deg = 180f / MathF.PI;

    /// <summary>极小量（浮点近似比较与零向量判定用，如 Vector3.Normalized）。</summary>
    public const float Epsilon = 1e-5f;

    /// <summary>将值钳制到 [min, max] 区间。</summary>
    /// <param name="value">原值。</param>
    /// <param name="min">下限。</param>
    /// <param name="max">上限。</param>
    /// <returns>value 落在区间内原样返回，否则返回最近的端点。</returns>
    public static float Clamp(float value, float min, float max)
    {
        if (value < min)
            return min;

        if (value > max)
            return max;

        return value;
    }

    /// <summary>将值钳制到 [0, 1] 区间。</summary>
    /// <param name="v">原值。</param>
    /// <returns>v 落在 [0, 1] 内原样返回，否则返回 0 或 1。</returns>
    public static float Clamp01(float v) => MathF.Max(0f, MathF.Min(1f, v));

    /// <summary>线性插值；t 自动钳制到 [0, 1]（Unity 语义，t 越界时返回端点值）。</summary>
    /// <param name="a">起点（t=0 时返回值）。</param>
    /// <param name="b">终点（t=1 时返回值）。</param>
    /// <param name="t">插值参数（越界时按 Clamp01 处理）。</param>
    /// <returns>a + (b - a) * Clamp01(t)。</returns>
    public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

    /// <summary>绝对值。</summary>
    /// <param name="v">原值。</param>
    /// <returns>v ≥ 0 时返回 v，否则返回 -v。</returns>
    public static float Abs(float v) => v < 0 ? -v : v;

    /// <summary>符号函数：正数返回 1，负数返回 -1，零返回 0。</summary>
    /// <param name="v">原值。</param>
    /// <returns>v 的符号（-1 / 0 / 1）。</returns>
    public static float Sign(float v) =>
        v > 0 ? 1f
        : v < 0 ? -1f
        : 0f;
}
