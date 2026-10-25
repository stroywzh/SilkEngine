using SilkEngine.Mathematics.Internal;

namespace SilkEngine.Mathematics;

/// <summary>
/// A 4-component vector, typically used for homogeneous coordinates or shader parameters.
/// </summary>
/// <remarks>
/// Pure value type: <c>readonly struct</c> with <c>public readonly</c> components, no in-place
/// mutators. <c>operator ==</c>, <see cref="Equals(Vector4)"/> and <see cref="GetHashCode"/> are
/// exact and mutually consistent; use <see cref="Approximately(in Vector4, in Vector4)"/> when a
/// tolerance is intended.
/// </remarks>
[MathValue("Vector4", "F3", "X", "Y", "Z", "W")]
public readonly partial struct Vector4 : IEquatable<Vector4>, IFormattable
{
    public readonly float X,
        Y,
        Z,
        W;

    public Vector4(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    public Vector4(float x, float y, float z)
        : this(x, y, z, 0f) { }

    public Vector4(float x, float y)
        : this(x, y, 0f, 0f) { }

    public Vector4(float x)
        : this(x, 0f, 0f, 0f) { }

    public static readonly Vector4 Zero = new(0f, 0f, 0f, 0f);
    public static readonly Vector4 One = new(1f, 1f, 1f, 1f);
    public static readonly Vector4 UnitX = new(1f, 0f, 0f, 0f);
    public static readonly Vector4 UnitY = new(0f, 1f, 0f, 0f);
    public static readonly Vector4 UnitZ = new(0f, 0f, 1f, 0f);
    public static readonly Vector4 UnitW = new(0f, 0f, 0f, 1f);

    /// <summary>Length of this vector.</summary>
    public float magnitude => MathF.Sqrt(sqrMagnitude);

    /// <summary>Squared length of this vector.</summary>
    public float sqrMagnitude => X * X + Y * Y + Z * Z + W * W;

    /// <summary>Returns a normalized copy of this vector.</summary>
    public Vector4 normalized => Normalize(this);

    /// <summary>Returns a normalized copy of the given vector.</summary>
    /// <param name="v">Vector to normalize.</param>
    /// <remarks>Returns <see cref="Zero"/> when the length is at or below <see cref="Approx.kEpsilon"/>.</remarks>
    public static Vector4 Normalize(Vector4 v)
    {
        float mag = v.magnitude;
        if (mag <= Approx.kEpsilon)
            return Zero;
        float inv = 1f / mag;
        return new Vector4(v.X * inv, v.Y * inv, v.Z * inv, v.W * inv);
    }

    /// <summary>Returns the component-wise product of two vectors.</summary>
    public static Vector4 Scale(Vector4 a, Vector4 b) =>
        new(a.X * b.X, a.Y * b.Y, a.Z * b.Z, a.W * b.W);

    /// <summary>Dot product of two vectors.</summary>
    public static float Dot(Vector4 a, Vector4 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;

    /// <summary>Euclidean distance between two points.</summary>
    public static float Distance(Vector4 a, Vector4 b) => (a - b).magnitude;

    /// <summary>Linearly interpolates between two vectors, with t clamped to [0, 1].</summary>
    /// <remarks>
    /// REV-016 口径：与根侧 Vector2/Vector3.Lerp 语义统一（同样的 [0,1] 夹取），但本层禁止引用
    /// EngineMath，故实现用 <see cref="System.Math.Clamp"/> 各写一份 —— 验收口径是「语义统一」，
    /// 不是「同一实现」。
    /// </remarks>
    public static Vector4 Lerp(Vector4 a, Vector4 b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Vector4(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t,
            a.Z + (b.Z - a.Z) * t,
            a.W + (b.W - a.W) * t
        );
    }

    /// <summary>Linearly interpolates between two vectors without clamping t.</summary>
    public static Vector4 LerpUnclamped(Vector4 a, Vector4 b, float t) =>
        new(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t,
            a.Z + (b.Z - a.Z) * t,
            a.W + (b.W - a.W) * t
        );

    /// <summary>Moves current toward target without exceeding maxDistanceDelta.</summary>
    /// <remarks>Uses the shared <see cref="Approx.kEpsilon"/> zero threshold, like Vector2/Vector3.</remarks>
    public static Vector4 MoveTowards(Vector4 current, Vector4 target, float maxDistanceDelta)
    {
        Vector4 to = target - current;
        float dist = to.magnitude;
        if (dist <= maxDistanceDelta || dist < Approx.kEpsilon)
            return target;
        return current + to / dist * maxDistanceDelta;
    }

    public static Vector4 Min(Vector4 a, Vector4 b) =>
        new(
            MathF.Min(a.X, b.X),
            MathF.Min(a.Y, b.Y),
            MathF.Min(a.Z, b.Z),
            MathF.Min(a.W, b.W)
        );

    public static Vector4 Max(Vector4 a, Vector4 b) =>
        new(
            MathF.Max(a.X, b.X),
            MathF.Max(a.Y, b.Y),
            MathF.Max(a.Z, b.Z),
            MathF.Max(a.W, b.W)
        );

    public static Vector4 operator +(Vector4 a, Vector4 b) =>
        new(a.X + b.X, a.Y + b.Y, a.Z + b.Z, a.W + b.W);

    public static Vector4 operator -(Vector4 a, Vector4 b) =>
        new(a.X - b.X, a.Y - b.Y, a.Z - b.Z, a.W - b.W);

    public static Vector4 operator -(Vector4 a) => new(-a.X, -a.Y, -a.Z, -a.W);

    public static Vector4 operator *(Vector4 a, float d) => new(a.X * d, a.Y * d, a.Z * d, a.W * d);

    public static Vector4 operator *(float d, Vector4 a) => new(a.X * d, a.Y * d, a.Z * d, a.W * d);

    public static Vector4 operator /(Vector4 a, float d) => new(a.X / d, a.Y / d, a.Z / d, a.W / d);

    /// <summary>
    /// Approximate equality with the shared relative-tolerance policy
    /// (see <see cref="Approx.Equal(float, float)"/>).
    /// </summary>
    public static bool Approximately(in Vector4 a, in Vector4 b) =>
        Approx.Equal(a.X, a.Y, a.Z, a.W, b.X, b.Y, b.Z, b.W);

}
