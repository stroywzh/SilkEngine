using SilkEngine.Mathematics;
using SilkEngine.Mathematics.Internal;

namespace SilkEngine;

/// <summary>
/// A 2D vector (typically the X/Y plane).
/// </summary>
/// <remarks>
/// Pure value type: <c>readonly struct</c> with <c>public readonly</c> components, no in-place
/// mutators. <c>operator ==</c>, <see cref="Equals(Vector2)"/> and <see cref="GetHashCode"/> are
/// exact and mutually consistent; use <see cref="Approximately(in Vector2, in Vector2)"/> when a
/// tolerance is intended.
/// </remarks>
[MathValue("Vector2", "F3", "X", "Y")]
public readonly partial struct Vector2 : IEquatable<Vector2>, IFormattable
{
    public readonly float X,
        Y;

    public Vector2(float x, float y)
    {
        X = x;
        Y = y;
    }

    public Vector2(float x)
        : this(x, 0) { }

    /// <summary>
    /// (0, 0)
    /// </summary>
    public static readonly Vector2 Zero = new(0, 0);

    /// <summary>
    /// (1f, 1f)
    /// </summary>
    public static readonly Vector2 One = new(1f, 1f);

    /// <summary>
    /// (1f, 0) — +X basis of the XY plane.
    /// </summary>
    /// <remarks>2D vectors use pure component names: the 2D plane is XY, so "up" here is +Y.</remarks>
    public static readonly Vector2 UnitX = new(1f, 0f);

    /// <summary>
    /// (0, 1f) — +Y basis of the XY plane.
    /// </summary>
    /// <remarks>Not the same axis as the 3D up (+Z); this is why the 3D names are not reused here.</remarks>
    public static readonly Vector2 UnitY = new(0f, 1f);

    /// <summary>Length of this vector.</summary>
    public float magnitude => EngineMath.Sqrt(sqrMagnitude);

    /// <summary>Squared length of this vector.</summary>
    public float sqrMagnitude => X * X + Y * Y;

    /// <summary>Returns a normalized copy of this vector.</summary>
    public Vector2 normalized => Normalize(this);

    /// <summary>Returns a normalized copy of the given vector.</summary>
    /// <param name="v">Vector to normalize.</param>
    /// <remarks>Returns <see cref="Zero"/> when the length is at or below <see cref="EngineMath.kEpsilon"/>.</remarks>
    public static Vector2 Normalize(Vector2 v)
    {
        float mag = v.magnitude;
        if (mag <= EngineMath.kEpsilon)
            return Zero;
        float inv = 1f / mag;
        return new Vector2(v.X * inv, v.Y * inv);
    }

    /// <summary>Returns the component-wise product of two vectors.</summary>
    public static Vector2 Scale(Vector2 a, Vector2 b) => new(a.X * b.X, a.Y * b.Y);

    /// <summary>Dot product of two vectors.</summary>
    public static float Dot(Vector2 lhs, Vector2 rhs) => lhs.X * rhs.X + lhs.Y * rhs.Y;

    /// <summary>Euclidean distance between two points.</summary>
    public static float Distance(Vector2 a, Vector2 b) => (a - b).magnitude;

    /// <summary>Linearly interpolates between two vectors, with t clamped to [0, 1].</summary>
    /// <remarks>
    /// REV-016 口径：与 Vector4.Lerp 语义统一（t 夹取到 [0,1]），实现各写一份 —— Mathematics 层不允许
    /// 引用 <see cref="EngineMath"/>。
    /// </remarks>
    public static Vector2 Lerp(Vector2 a, Vector2 b, float t)
    {
        t = EngineMath.Clamp01(t);
        return new Vector2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    }

    /// <summary>Linearly interpolates between two vectors without clamping t.</summary>
    public static Vector2 LerpUnclamped(Vector2 a, Vector2 b, float t) =>
        new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    /// <summary>Moves current toward target without exceeding maxDistanceDelta.</summary>
    public static Vector2 MoveTowards(Vector2 current, Vector2 target, float maxDistanceDelta)
    {
        Vector2 to = target - current;
        float dist = to.magnitude;
        if (dist <= maxDistanceDelta || dist < EngineMath.kEpsilon)
            return target;
        return current + to / dist * maxDistanceDelta;
    }

    /// <summary>Returns a copy of vector with its magnitude clamped to maxLength.</summary>
    public static Vector2 ClampMagnitude(Vector2 vector, float maxLength)
    {
        float sqr = vector.sqrMagnitude;
        if (sqr > maxLength * maxLength)
        {
            float mag = EngineMath.Sqrt(sqr);
            return vector / mag * maxLength;
        }
        return vector;
    }

    /// <summary>Reflects a vector off a plane defined by a normal.</summary>
    public static Vector2 Reflect(Vector2 inDirection, Vector2 inNormal) =>
        -2f * Dot(inNormal, inDirection) * inNormal + inDirection;

    /// <summary>Returns the perpendicular of a vector (rotated 90° counter-clockwise).</summary>
    public static Vector2 Perpendicular(Vector2 inDirection) => new(-inDirection.Y, inDirection.X);

    /// <summary>Angle in degrees between two vectors (0..180).</summary>
    public static float Angle(Vector2 from, Vector2 to)
    {
        float denom = EngineMath.Sqrt(from.sqrMagnitude * to.sqrMagnitude);
        if (denom < EngineMath.kEpsilon)
            return 0f;
        float dot = EngineMath.Max(EngineMath.Min(Dot(from, to) / denom, 1f), -1f);
        return EngineMath.Acos(dot) * EngineMath.Rad2Deg;
    }

    /// <summary>Signed angle in degrees between two vectors (counter-clockwise positive).</summary>
    public static float SignedAngle(Vector2 from, Vector2 to)
    {
        float unsigned = Angle(from, to);
        float cross = from.X * to.Y - from.Y * to.X;
        return unsigned * EngineMath.Sign(cross);
    }

    public static Vector2 Min(Vector2 a, Vector2 b) =>
        new(EngineMath.Min(a.X, b.X), EngineMath.Min(a.Y, b.Y));

    public static Vector2 Max(Vector2 a, Vector2 b) =>
        new(EngineMath.Max(a.X, b.X), EngineMath.Max(a.Y, b.Y));

    /// <summary>
    /// Approximate equality with the shared relative-tolerance policy
    /// (see <see cref="EngineMath.Approximately(float, float)"/>).
    /// </summary>
    public static bool Approximately(in Vector2 a, in Vector2 b) =>
        Approx.Equal(a.X, a.Y, b.X, b.Y);

    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);

    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);

    public static Vector2 operator -(Vector2 a) => new(-a.X, -a.Y);

    public static Vector2 operator *(Vector2 a, float d) => new(a.X * d, a.Y * d);

    public static Vector2 operator *(float d, Vector2 a) => new(a.X * d, a.Y * d);

    public static Vector2 operator /(Vector2 a, float d) => new(a.X / d, a.Y / d);

}
