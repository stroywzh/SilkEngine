namespace SilkEngine;

/// <summary>
/// A 2D vector (typically the X/Y plane).
/// </summary>
public struct Vector2 : IEquatable<Vector2>
{
    public float X,
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
    /// (0, 1f)
    /// </summary>
    public static readonly Vector2 Up = new(0f, 1f);

    /// <summary>
    /// (0, -1f)
    /// </summary>
    public static readonly Vector2 Down = new(0f, -1f);

    /// <summary>
    /// (-1f, 0)
    /// </summary>
    public static readonly Vector2 Left = new(-1f, 0f);

    /// <summary>
    /// (1f, 0)
    /// </summary>
    public static readonly Vector2 Right = new(1f, 0f);

    /// <summary>Length of this vector.</summary>
    public float magnitude => MathF.Sqrt(sqrMagnitude);

    /// <summary>Squared length of this vector.</summary>
    public float sqrMagnitude => X * X + Y * Y;

    /// <summary>Returns a normalized copy of this vector.</summary>
    public Vector2 normalized => Normalize(this);

    public float this[int i]
    {
        get =>
            i switch
            {
                0 => X,
                1 => Y,
                _ => throw new IndexOutOfRangeException(),
            };
        set
        {
            if (i == 0)
                X = value;
            else if (i == 1)
                Y = value;
            else
                throw new IndexOutOfRangeException();
        }
    }

    /// <summary>Returns a normalized copy of the given vector.</summary>
    public static Vector2 Normalize(Vector2 v)
    {
        v.Normalize();
        return v;
    }

    /// <summary>Normalizes this vector in place.</summary>
    public void Normalize()
    {
        float mag = magnitude;
        if (mag > EngineMath.kEpsilon)
        {
            X /= mag;
            Y /= mag;
        }
        else
            this = Zero;
    }

    public void Scale(Vector2 scale)
    {
        X *= scale.X;
        Y *= scale.Y;
    }

    public static Vector2 Scale(Vector2 a, Vector2 b) => new(a.X * b.X, a.Y * b.Y);

    /// <summary>Dot product of two vectors.</summary>
    public static float Dot(Vector2 lhs, Vector2 rhs) => lhs.X * rhs.X + lhs.Y * rhs.Y;

    /// <summary>Euclidean distance between two points.</summary>
    public static float Distance(Vector2 a, Vector2 b) => (a - b).magnitude;

    /// <summary>Linearly interpolates between two vectors.</summary>
    public static Vector2 Lerp(Vector2 a, Vector2 b, float t)
    {
        t = t < 0f ? 0f : (t > 1f ? 1f : t);
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
            float mag = MathF.Sqrt(sqr);
            return vector / mag * maxLength;
        }
        return vector;
    }

    /// <summary>Reflects a vector off a plane defined by a normal.</summary>
    public static Vector2 Reflect(Vector2 inDirection, Vector2 inNormal) =>
        -2f * Dot(inNormal, inDirection) * inNormal + inDirection;

    /// <summary>Returns the perpendicular of this vector (rotated 90° counter-clockwise).</summary>
    public static Vector2 Perpendicular(Vector2 inDirection) => new(-inDirection.Y, inDirection.X);

    /// <summary>Angle in degrees between two vectors (0..180).</summary>
    public static float Angle(Vector2 from, Vector2 to)
    {
        float denom = MathF.Sqrt(from.sqrMagnitude * to.sqrMagnitude);
        if (denom < EngineMath.kEpsilon)
            return 0f;
        float dot = MathF.Max(MathF.Min(Dot(from, to) / denom, 1f), -1f);
        return MathF.Acos(dot) * EngineMath.Rad2Deg;
    }

    /// <summary>Signed angle in degrees between two vectors (counter-clockwise positive).</summary>
    public static float SignedAngle(Vector2 from, Vector2 to)
    {
        float unsigned = Angle(from, to);
        float cross = from.X * to.Y - from.Y * to.X;
        return unsigned * MathF.Sign(cross);
    }

    public static Vector2 Min(Vector2 a, Vector2 b) =>
        new(EngineMath.Min(a.X, b.X), EngineMath.Min(a.Y, b.Y));

    public static Vector2 Max(Vector2 a, Vector2 b) =>
        new(EngineMath.Max(a.X, b.X), EngineMath.Max(a.Y, b.Y));

    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);

    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);

    public static Vector2 operator -(Vector2 a) => new(-a.X, -a.Y);

    public static Vector2 operator *(Vector2 a, float d) => new(a.X * d, a.Y * d);

    public static Vector2 operator *(float d, Vector2 a) => new(a.X * d, a.Y * d);

    public static Vector2 operator /(Vector2 a, float d) => new(a.X / d, a.Y / d);

    public static bool operator ==(Vector2 lhs, Vector2 rhs) =>
        ApproxEquals.Equal(lhs.X, lhs.Y, rhs.X, rhs.Y);

    public static bool operator !=(Vector2 lhs, Vector2 rhs) => !(lhs == rhs);

    public bool Equals(Vector2 other) => this == other;

    public override bool Equals(object? other) => other is Vector2 v && this == v;

    public override int GetHashCode() => HashUtil.Of(X, Y);

    public override string ToString() => ToString(null, null);

    public string ToString(string format) => ToString(format, null);

    /// <summary>Formats the vector using an optional numeric format and culture, e.g. <c>v.ToString("F4")</c>.</summary>
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        FormatUtil.Format("F2", format, formatProvider, X, Y);
}
