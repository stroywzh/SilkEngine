using SilkEngine.Mathematics;
using SilkEngine.Mathematics.Internal;

namespace SilkEngine;

/// <summary>
/// A 3D vector. Coordinate convention follows Unreal Engine: +X is Forward, +Y is Right, +Z is Up.
/// </summary>
/// <remarks>
/// Pure value type: <c>readonly struct</c> with <c>public readonly</c> components, no in-place
/// mutators. <c>operator ==</c>, <see cref="Equals(Vector3)"/> and <see cref="GetHashCode"/> are
/// exact and mutually consistent; use <see cref="Approximately(in Vector3, in Vector3)"/> when a
/// tolerance is intended.
/// </remarks>
[MathValue("Vector3", "F3", "X", "Y", "Z")]
public readonly partial struct Vector3 : IEquatable<Vector3>, IFormattable
{
    public readonly float X,
        Y,
        Z;

    public Vector3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public Vector3(float x)
        : this(x, 0, 0) { }

    public Vector3(float x, float y)
        : this(x, y, 0) { }

    /// <summary>
    /// (0, 0, 0)
    /// </summary>
    public static readonly Vector3 Zero = new(0f, 0f, 0f);

    /// <summary>
    /// (1f, 1f, 1f)
    /// </summary>
    public static readonly Vector3 One = new(1f, 1f, 1f);

    /// <summary>
    /// (1f, 0, 0) — unit basis vector along +X.
    /// </summary>
    public static readonly Vector3 UnitX = new(1f, 0f, 0f);

    /// <summary>
    /// (0, 1f, 0) — unit basis vector along +Y.
    /// </summary>
    public static readonly Vector3 UnitY = new(0f, 1f, 0f);

    /// <summary>
    /// (0, 0, 1f) — unit basis vector along +Z.
    /// </summary>
    public static readonly Vector3 UnitZ = new(0f, 0f, 1f);

    /// <summary>
    /// (1f, 0, 0)
    /// </summary>
    public static readonly Vector3 Forward = new(1f, 0f, 0f);

    /// <summary>
    /// (-1f, 0, 0)
    /// </summary>
    public static readonly Vector3 Back = new(-1f, 0f, 0f);

    /// <summary>
    /// (0, 1f, 0)
    /// </summary>
    public static readonly Vector3 Right = new(0f, 1f, 0f);

    /// <summary>
    /// (0, -1f, 0)
    /// </summary>
    public static readonly Vector3 Left = new(0f, -1f, 0f);

    /// <summary>
    /// (0, 0, 1f)
    /// </summary>
    public static readonly Vector3 Up = new(0f, 0f, 1f);

    /// <summary>
    /// (0, 0, -1f)
    /// </summary>
    public static readonly Vector3 Down = new(0f, 0f, -1f);

    /// <summary>Length of this vector.</summary>
    public float magnitude => EngineMath.Sqrt(sqrMagnitude);

    /// <summary>Squared length of this vector.</summary>
    public float sqrMagnitude => X * X + Y * Y + Z * Z;

    /// <summary>Returns a normalized copy of this vector.</summary>
    public Vector3 normalized => Normalize(this);

    /// <summary>Returns a normalized copy of the given vector.</summary>
    /// <param name="v">Vector to normalize.</param>
    /// <remarks>Returns <see cref="Zero"/> when the length is at or below <see cref="EngineMath.kEpsilon"/>.</remarks>
    public static Vector3 Normalize(Vector3 v)
    {
        float mag = v.magnitude;
        if (mag <= EngineMath.kEpsilon)
            return Zero;
        float inv = 1f / mag;
        return new Vector3(v.X * inv, v.Y * inv, v.Z * inv);
    }

    /// <summary>Returns the component-wise product of two vectors.</summary>
    public static Vector3 Scale(Vector3 a, Vector3 b) => new(a.X * b.X, a.Y * b.Y, a.Z * b.Z);

    /// <summary>Dot product of two vectors.</summary>
    public static float Dot(Vector3 lhs, Vector3 rhs) =>
        lhs.X * rhs.X + lhs.Y * rhs.Y + lhs.Z * rhs.Z;

    /// <summary>Cross product of two vectors.</summary>
    public static Vector3 Cross(Vector3 lhs, Vector3 rhs) =>
        new(
            lhs.Y * rhs.Z - lhs.Z * rhs.Y,
            lhs.Z * rhs.X - lhs.X * rhs.Z,
            lhs.X * rhs.Y - lhs.Y * rhs.X
        );

    /// <summary>Euclidean distance between two points.</summary>
    public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;

    /// <summary>Linearly interpolates between two vectors, with t clamped to [0, 1].</summary>
    public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
    {
        t = EngineMath.Clamp01(t);
        return new Vector3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
    }

    /// <summary>Linearly interpolates between two vectors without clamping t.</summary>
    public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) =>
        new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);

    /// <summary>Spherically interpolates between two direction vectors.</summary>
    /// <param name="a">Start direction; must be non-zero.</param>
    /// <param name="b">End direction; must be non-zero.</param>
    /// <param name="t">Interpolation factor, clamped to [0, 1].</param>
    /// <returns>
    /// A unit-length direction: <paramref name="a"/>'s direction at t = 0 and <paramref name="b"/>'s
    /// direction at t = 1. 
    /// </returns>
    /// <remarks>
    /// When the inputs are (anti)parallel the rotation plane is undefined; the result then stays on
    /// the great circle picked by <see cref="Normalize(Vector3)"/> of the zero residual, i.e. it
    /// degrades to the start direction scaled by cos(theta).
    /// </remarks>
    public static Vector3 Slerp(Vector3 a, Vector3 b, float t)
    {
        t = EngineMath.Clamp01(t);

        Vector3 from = Normalize(a);
        Vector3 to = Normalize(b);
        float dot = EngineMath.Max(EngineMath.Min(Dot(from, to), 1f), -1f);
        float theta = EngineMath.Acos(dot) * t;
        Vector3 rel = Normalize(to - from * dot);
        return from * EngineMath.Cos(theta) + rel * EngineMath.Sin(theta);
    }

    /// <summary>Moves current toward target without exceeding maxDistanceDelta.</summary>
    public static Vector3 MoveTowards(Vector3 current, Vector3 target, float maxDistanceDelta)
    {
        Vector3 to = target - current;
        float dist = to.magnitude;
        if (dist <= maxDistanceDelta || dist < EngineMath.kEpsilon)
            return target;
        return current + to / dist * maxDistanceDelta;
    }

    /// <summary>Returns a copy of vector with its magnitude clamped to maxLength.</summary>
    public static Vector3 ClampMagnitude(Vector3 vector, float maxLength)
    {
        float sqr = vector.sqrMagnitude;
        if (sqr > maxLength * maxLength)
        {
            float mag = EngineMath.Sqrt(sqr);
            return vector / mag * maxLength;
        }
        return vector;
    }

    /// <summary>Projects a vector onto another vector.</summary>
    public static Vector3 Project(Vector3 vector, Vector3 onNormal)
    {
        float sqr = Dot(onNormal, onNormal);
        if (sqr < EngineMath.kEpsilon)
            return Zero;
        return onNormal * (Dot(vector, onNormal) / sqr);
    }

    /// <summary>Projects a vector onto a plane defined by a normal.</summary>
    public static Vector3 ProjectOnPlane(Vector3 vector, Vector3 planeNormal) =>
        vector - Project(vector, planeNormal);

    /// <summary>Reflects a vector off a plane defined by a normal.</summary>
    public static Vector3 Reflect(Vector3 inDirection, Vector3 inNormal) =>
        -2f * Dot(inNormal, inDirection) * inNormal + inDirection;

    /// <summary>Angle in degrees between two vectors (0..180).</summary>
    public static float Angle(Vector3 from, Vector3 to)
    {
        float denom = EngineMath.Sqrt(from.sqrMagnitude * to.sqrMagnitude);
        if (denom < EngineMath.kEpsilon)
            return 0f;
        float dot = EngineMath.Max(EngineMath.Min(Dot(from, to) / denom, 1f), -1f);
        return EngineMath.Acos(dot) * EngineMath.Rad2Deg;
    }

    /// <summary>Signed angle in degrees between two vectors around the given axis.</summary>
    public static float SignedAngle(Vector3 from, Vector3 to, Vector3 axis)
    {
        float unsigned = Angle(from, to);
        float sign = EngineMath.Sign(Dot(axis, Cross(from, to)));
        return unsigned * sign;
    }

    public static Vector3 Min(Vector3 a, Vector3 b) =>
        new(EngineMath.Min(a.X, b.X), EngineMath.Min(a.Y, b.Y), EngineMath.Min(a.Z, b.Z));

    public static Vector3 Max(Vector3 a, Vector3 b) =>
        new(EngineMath.Max(a.X, b.X), EngineMath.Max(a.Y, b.Y), EngineMath.Max(a.Z, b.Z));

    /// <summary>
    /// Approximate equality with the shared relative-tolerance policy
    /// (see <see cref="EngineMath.Approximately(float, float)"/>).
    /// </summary>
    public static bool Approximately(in Vector3 a, in Vector3 b) =>
        Approx.Equal(a.X, a.Y, a.Z, b.X, b.Y, b.Z);

    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Vector3 operator -(Vector3 a) => new(-a.X, -a.Y, -a.Z);

    public static Vector3 operator *(Vector3 a, float d) => new(a.X * d, a.Y * d, a.Z * d);

    public static Vector3 operator *(float d, Vector3 a) => new(a.X * d, a.Y * d, a.Z * d);

    public static Vector3 operator /(Vector3 a, float d) => new(a.X / d, a.Y / d, a.Z / d);

    /// <summary>Explicit Vector3 → Vector4 conversion (w = 0).</summary>
    /// <remarks>
    /// Declared on the root side because <see cref="Vector4"/> lives in
    /// <c>SilkEngine.Mathematics</c> and must not reference the root namespace (layering R4).
    /// </remarks>
    public static explicit operator Vector4(Vector3 v) => new(v.X, v.Y, v.Z, 0f);

    /// <summary>Explicit Vector4 → Vector3 conversion (drops W).</summary>
    /// <remarks>Declared on the root side for the same layering reason as the conversion above.</remarks>
    public static explicit operator Vector3(Vector4 v) => new(v.X, v.Y, v.Z);

}
