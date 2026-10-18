using System;

namespace SilkEngine;

/// <summary>
/// Math utility library, modeled after UnityEngine.Mathf.
/// Angles are in degrees. Methods with a "Deg" suffix take/return degrees;
/// all other trigonometric methods take/return radians.
/// </summary>
public static class EngineMath
{
    /// <summary>Default tolerance for approximate float comparison.</summary>
    public const float kEpsilon = 0.00001f;

    /// <summary>Tiny value used to compare "squared length near zero" without underflowing before sqrt.</summary>
    public const float kEpsilonNormalSqrt = 1e-15f;

    /// <summary>Pi (π).</summary>
    public const float PI = MathF.PI;

    /// <summary>Two pi (2π), one full turn.</summary>
    public const float TwoPI = MathF.PI * 2f;

    /// <summary>Half pi (π/2), a quarter turn.</summary>
    public const float HalfPI = MathF.PI * 0.5f;

    /// <summary>Degrees → radians multiplier.</summary>
    public const float Deg2Rad = MathF.PI / 180f;

    /// <summary>Radians → degrees multiplier.</summary>
    public const float Rad2Deg = 180f / MathF.PI;

    /// <summary>Euler's number e.</summary>
    public const float E = MathF.E;

    /// <summary>Positive infinity.</summary>
    public const float Infinity = float.PositiveInfinity;

    /// <summary>Negative infinity.</summary>
    public const float NegativeInfinity = float.NegativeInfinity;

    /// <summary>
    /// Relative-tolerance approximate equality.
    /// Returns true when |a-b| is below a magnitude-scaled tolerance;
    /// large values get more slack, values near zero use the floor kEpsilon*8.
    /// </summary>
    public static bool Approximately(float a, float b) =>
        MathF.Abs(b - a)
        < MathF.Max(kEpsilon * MathF.Max(MathF.Abs(a), MathF.Abs(b)), kEpsilon * 8f);

    /// <summary>Absolute-tolerance approximate equality: |a-b| &lt; epsilon.</summary>
    public static bool Approximately(float a, float b, float epsilon) => MathF.Abs(a - b) < epsilon;

    /// <summary>Absolute value (float).</summary>
    public static float Abs(float v) => MathF.Abs(v);

    /// <summary>Absolute value (int).</summary>
    public static int Abs(int v) => Math.Abs(v);

    /// <summary>Sign of v: returns 1 when v &gt;= 0, otherwise -1 (note: 0 returns +1).</summary>
    public static float Sign(float v) => v >= 0f ? 1f : -1f;

    /// <summary>Sign of v: returns 1 when v &gt;= 0, otherwise -1 (note: 0 returns +1).</summary>
    public static int Sign(int v) => v >= 0 ? 1 : -1;

    /// <summary>Smaller of two floats.</summary>
    public static float Min(float a, float b) => a < b ? a : b;

    /// <summary>Larger of two floats.</summary>
    public static float Max(float a, float b) => a > b ? a : b;

    /// <summary>Smaller of two ints.</summary>
    public static int Min(int a, int b) => a < b ? a : b;

    /// <summary>Larger of two ints.</summary>
    public static int Max(int a, int b) => a > b ? a : b;

    /// <summary>Square root.</summary>
    public static float Sqrt(float v) => MathF.Sqrt(v);

    /// <summary>Power: a raised to b.</summary>
    public static float Pow(float a, float b) => MathF.Pow(a, b);

    /// <summary>Largest integer &lt;= v (returned as float).</summary>
    public static float Floor(float v) => MathF.Floor(v);

    /// <summary>Smallest integer &gt;= v (returned as float).</summary>
    public static float Ceil(float v) => MathF.Ceiling(v);

    /// <summary>Rounds to nearest integer (banker's rounding by default).</summary>
    public static float Round(float v) => MathF.Round(v);

    /// <summary>Floors to the nearest int.</summary>
    public static int FloorToInt(float v) => (int)MathF.Floor(v);

    /// <summary>Ceils to the nearest int.</summary>
    public static int CeilToInt(float v) => (int)MathF.Ceiling(v);

    /// <summary>Rounds to the nearest int.</summary>
    public static int RoundToInt(float v) => (int)MathF.Round(v);

    /// <summary>Sine (radians).</summary>
    public static float Sin(float v) => MathF.Sin(v);

    /// <summary>Cosine (radians).</summary>
    public static float Cos(float v) => MathF.Cos(v);

    /// <summary>Tangent (radians).</summary>
    public static float Tan(float v) => MathF.Tan(v);

    /// <summary>Arcsine (radians).</summary>
    public static float Asin(float v) => MathF.Asin(v);

    /// <summary>Arccosine (radians).</summary>
    public static float Acos(float v) => MathF.Acos(v);

    /// <summary>Arctangent (radians).</summary>
    public static float Atan(float v) => MathF.Atan(v);

    /// <summary>Two-argument arctangent (radians). Returns the angle whose tangent is y/x.</summary>
    public static float Atan2(float y, float x) => MathF.Atan2(y, x);

    /// <summary>Sine (input in degrees).</summary>
    public static float SinDeg(float deg) => MathF.Sin(deg * Deg2Rad);

    /// <summary>Cosine (input in degrees).</summary>
    public static float CosDeg(float deg) => MathF.Cos(deg * Deg2Rad);

    /// <summary>Tangent (input in degrees).</summary>
    public static float TanDeg(float deg) => MathF.Tan(deg * Deg2Rad);

    /// <summary>Arcsine (output in degrees).</summary>
    public static float AsinDeg(float v) => MathF.Asin(v) * Rad2Deg;

    /// <summary>Arccosine (output in degrees).</summary>
    public static float AcosDeg(float v) => MathF.Acos(v) * Rad2Deg;

    /// <summary>Arctangent (output in degrees).</summary>
    public static float AtanDeg(float v) => MathF.Atan(v) * Rad2Deg;

    /// <summary>Two-argument arctangent (output in degrees).</summary>
    public static float Atan2Deg(float y, float x) => MathF.Atan2(y, x) * Rad2Deg;

    /// <summary>e raised to v.</summary>
    public static float Exp(float v) => MathF.Exp(v);

    /// <summary>Natural logarithm.</summary>
    public static float Log(float v) => MathF.Log(v);

    /// <summary>Logarithm of v in base b.</summary>
    public static float Log(float v, float b) => MathF.Log(v, b);

    /// <summary>Base-10 logarithm.</summary>
    public static float Log10(float v) => MathF.Log10(v);

    /// <summary>Clamps v into [min, max].</summary>
    public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);

    /// <summary>Clamps v into [min, max].</summary>
    public static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);

    /// <summary>Clamps v into [0, 1].</summary>
    public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

    /// <summary>Linear interpolation between a and b, with t clamped to [0, 1].</summary>
    public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

    /// <summary>Linear interpolation without clamping t (extrapolation allowed).</summary>
    public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;

    /// <summary>
    /// Inverse of <see cref="Lerp"/>: returns the t in [0, 1] that produced
    /// <paramref name="value"/> between a and b, or 0 when a == b.
    /// </summary>
    public static float InverseLerp(float a, float b, float value) =>
        a != b ? Clamp01((value - a) / (b - a)) : 0f;

    /// <summary>
    /// Smooth Hermite interpolation between from and to.
    /// Applies a smoothstep curve to t (clamped to [0, 1]) for ease-in/ease-out.
    /// </summary>
    public static float SmoothStep(float from, float to, float t)
    {
        t = Clamp01(t);
        t = t * t * (3f - 2f * t);
        return to * t + from * (1f - t);
    }

    /// <summary>
    /// Moves <paramref name="current"/> toward <paramref name="target"/> by at most
    /// <paramref name="maxDelta"/>. Snaps to target when within range.
    /// </summary>
    public static float MoveTowards(float current, float target, float maxDelta)
    {
        if (MathF.Abs(target - current) <= maxDelta)
            return target;
        return current + Sign(target - current) * maxDelta;
    }

    /// <summary>
    /// Repeats t inside [0, length) — the floating-point analog of modulo
    /// that always returns a positive result.
    /// </summary>
    public static float Repeat(float t, float length) =>
        Clamp(t - MathF.Floor(t / length) * length, 0f, length);

    /// <summary>
    /// Ping-pongs t back and forth between 0 and <paramref name="length"/>.
    /// e.g. length = 2 → t: 0 → 2 → 0 → 2 …
    /// </summary>
    public static float PingPong(float t, float length)
    {
        t = Repeat(t, length * 2f);
        return length - MathF.Abs(t - length);
    }
}
