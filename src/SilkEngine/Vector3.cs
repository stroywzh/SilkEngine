namespace SilkEngine;

/// <summary>
/// A 3D vector. Coordinate convention follows Unreal Engine: +X is Forward, +Y is Right, +Z is Up.
/// </summary>
public struct Vector3 : IEquatable<Vector3>
{
    public float X,
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

    public float this[int i]
    {
        get =>
            i switch
            {
                0 => X,
                1 => Y,
                2 => Z,
                _ => throw new IndexOutOfRangeException(),
            };
        set
        {
            switch (i)
            {
                case 0:
                    X = value;
                    break;
                case 1:
                    Y = value;
                    break;
                case 2:
                    Z = value;
                    break;
                default:
                    throw new IndexOutOfRangeException();
            }
        }
    }

    /// <summary>Normalizes this vector in place.</summary>
    public void Normalize()
    {
        float mag = magnitude;
        if (mag > EngineMath.kEpsilon)
        {
            X /= mag;
            Y /= mag;
            Z /= mag;
        }
        else
            this = Zero;
    }

    /// <summary>Returns a normalized copy of the given vector.</summary>
    public static Vector3 Normalize(Vector3 v)
    {
        v.Normalize();
        return v;
    }

    /// <summary>Multiplies two vectors component-wise.</summary>
    public void Scale(Vector3 scale)
    {
        X *= scale.X;
        Y *= scale.Y;
        Z *= scale.Z;
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

    /// <summary>Linearly interpolates between two vectors.</summary>
    public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
    {
        t = t < 0f ? 0f : (t > 1f ? 1f : t);
        return new Vector3(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
    }

    /// <summary>Linearly interpolates between two vectors without clamping t.</summary>
    public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) =>
        new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);

    /// <summary>Spherically interpolates between two direction vectors.</summary>
    public static Vector3 Slerp(Vector3 a, Vector3 b, float t)
    {
        t = EngineMath.Clamp01(t);
        float dot = EngineMath.Max(EngineMath.Min(Dot(a.normalized, b.normalized), 1f), -1f);
        float theta = EngineMath.Acos(dot) * t;
        Vector3 rel = Normalize(b - a * dot);
        return a * EngineMath.Cos(theta) + rel * EngineMath.Sin(theta);
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

    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Vector3 operator -(Vector3 a) => new(-a.X, -a.Y, -a.Z);

    public static Vector3 operator *(Vector3 a, float d) => new(a.X * d, a.Y * d, a.Z * d);

    public static Vector3 operator *(float d, Vector3 a) => new(a.X * d, a.Y * d, a.Z * d);

    public static Vector3 operator /(Vector3 a, float d) => new(a.X / d, a.Y / d, a.Z / d);

    public static bool operator !=(Vector3 lhs, Vector3 rhs) => !(lhs == rhs);

    public bool Equals(Vector3 other) => X == other.X && Y == other.Y && Z == other.Z;

    public override int GetHashCode() => HashUtil.Of(X, Y, Z);

    public static bool operator ==(Vector3 lhs, Vector3 rhs) =>
        ApproxEquals.Equal(lhs.X, lhs.Y, lhs.Z, rhs.X, rhs.Y, rhs.Z);

    public override bool Equals(object? obj) => obj is Vector3 v && Equals(v);

    public override string ToString() => ToString(null, null);

    public string ToString(string format) => ToString(format, null);

    public string ToString(string? format, IFormatProvider? formatProvider) =>
        FormatUtil.Format("F2", format, formatProvider, X, Y, Z);
}
