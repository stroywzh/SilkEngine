namespace SilkEngine.Mathematics.Internal;

/// <summary>
/// The single approximate-equality policy shared by every math value type. Comparison is
/// relative: the tolerance scales with the compared magnitudes and never drops below
/// <see cref="kEpsilon"/> * 8, so values near zero still have a defined floor.
/// Component-wise overloads are the conjunction of the per-component comparisons.
/// <para>
/// This is deliberately independent of <c>operator ==</c>: equality operators are exact and
/// transitive, tolerance comparison is opt-in and must be requested by name.
/// </para>
/// </summary>
/// <remarks>
/// The tolerance primitive lives here — in the lowest layer — so that the math namespace can
/// compare components without referencing the root math facade. The root
/// <c>EngineMath.kEpsilon</c> constant forwards to this one, so the value has a single definition.
/// </remarks>
internal static class Approx
{
    /// <summary>Base tolerance of the engine; the comparison floor is 8× this value.</summary>
    public const float kEpsilon = 0.00001f;

    /// <summary>
    /// Relative-tolerance comparison of two components; the root <c>EngineMath.Approximately</c>
    /// facade forwards here.
    /// </summary>
    public static bool Equal(float a, float b) =>
        MathF.Abs(b - a) < MathF.Max(kEpsilon * MathF.Max(MathF.Abs(a), MathF.Abs(b)), kEpsilon * 8f);

    public static bool Equal(float a0, float a1, float b0, float b1) =>
        Equal(a0, b0) && Equal(a1, b1);

    public static bool Equal(float a0, float a1, float a2, float b0, float b1, float b2) =>
        Equal(a0, b0) && Equal(a1, b1) && Equal(a2, b2);

    public static bool Equal(
        float a0,
        float a1,
        float a2,
        float a3,
        float b0,
        float b1,
        float b2,
        float b3
    ) => Equal(a0, b0) && Equal(a1, b1) && Equal(a2, b2) && Equal(a3, b3);
}
