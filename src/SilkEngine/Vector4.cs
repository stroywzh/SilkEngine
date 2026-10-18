namespace SilkEngine;

/// <summary>
/// A 4-component vector, typically used for homogeneous coordinates or shader parameters.
/// </summary>
public struct Vector4 : IEquatable<Vector4>, IFormattable
{
    public float X,
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

    public float magnitude => EngineMath.Sqrt(sqrMagnitude);
    public float sqrMagnitude => X * X + Y * Y + Z * Z + W * W;
    public Vector4 normalized => Normalize(this);

    public float this[int i]
    {
        get =>
            i switch
            {
                0 => X,
                1 => Y,
                2 => Z,
                3 => W,
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
                case 3:
                    W = value;
                    break;
                default:
                    throw new IndexOutOfRangeException();
            }
        }
    }

    public void Normalize()
    {
        float mag = magnitude;
        if (mag > EngineMath.kEpsilon)
        {
            X /= mag;
            Y /= mag;
            Z /= mag;
            W /= mag;
        }
        else
            this = Zero;
    }

    public static Vector4 Normalize(Vector4 v)
    {
        v.Normalize();
        return v;
    }

    public void Scale(Vector4 scale)
    {
        X *= scale.X;
        Y *= scale.Y;
        Z *= scale.Z;
        W *= scale.W;
    }

    public static Vector4 Scale(Vector4 a, Vector4 b) =>
        new(a.X * b.X, a.Y * b.Y, a.Z * b.Z, a.W * b.W);

    public static float Dot(Vector4 a, Vector4 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;

    public static float Distance(Vector4 a, Vector4 b) => (a - b).magnitude;

    public static Vector4 Lerp(Vector4 a, Vector4 b, float t)
    {
        t = EngineMath.Clamp01(t);
        return new Vector4(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t,
            a.Z + (b.Z - a.Z) * t,
            a.W + (b.W - a.W) * t
        );
    }

    public static Vector4 LerpUnclamped(Vector4 a, Vector4 b, float t) =>
        new(
            a.X + (b.X - a.X) * t,
            a.Y + (b.Y - a.Y) * t,
            a.Z + (b.Z - a.Z) * t,
            a.W + (b.W - a.W) * t
        );

    public static Vector4 MoveTowards(Vector4 current, Vector4 target, float maxDistanceDelta)
    {
        Vector4 to = target - current;
        float dist = to.magnitude;
        if (dist <= maxDistanceDelta || dist == 0f)
            return target;
        return current + to / dist * maxDistanceDelta;
    }

    public static Vector4 Min(Vector4 a, Vector4 b) =>
        new(
            EngineMath.Min(a.X, b.X),
            EngineMath.Min(a.Y, b.Y),
            EngineMath.Min(a.Z, b.Z),
            EngineMath.Min(a.W, b.W)
        );

    public static Vector4 Max(Vector4 a, Vector4 b) =>
        new(
            EngineMath.Max(a.X, b.X),
            EngineMath.Max(a.Y, b.Y),
            EngineMath.Max(a.Z, b.Z),
            EngineMath.Max(a.W, b.W)
        );

    /// <summary>Implicit Vector3 -> Vector4 (w = 0).</summary>
    public static implicit operator Vector4(Vector3 v) => new(v.X, v.Y, v.Z, 0f);

    /// <summary>Explicit Vector4 -> Vector3 (drops W).</summary>
    public static explicit operator Vector3(Vector4 v) => new(v.X, v.Y, v.Z);

    public static Vector4 operator +(Vector4 a, Vector4 b) =>
        new(a.X + b.X, a.Y + b.Y, a.Z + b.Z, a.W + b.W);

    public static Vector4 operator -(Vector4 a, Vector4 b) =>
        new(a.X - b.X, a.Y - b.Y, a.Z - b.Z, a.W - b.W);

    public static Vector4 operator -(Vector4 a) => new(-a.X, -a.Y, -a.Z, -a.W);

    public static Vector4 operator *(Vector4 a, float d) => new(a.X * d, a.Y * d, a.Z * d, a.W * d);

    public static Vector4 operator *(float d, Vector4 a) => new(a.X * d, a.Y * d, a.Z * d, a.W * d);

    public static Vector4 operator /(Vector4 a, float d) => new(a.X / d, a.Y / d, a.Z / d, a.W / d);

    public static bool operator ==(Vector4 lhs, Vector4 rhs) =>
        ApproxEquals.Equal(lhs.X, lhs.Y, lhs.Z, lhs.W, rhs.X, rhs.Y, rhs.Z, rhs.W);

    public static bool operator !=(Vector4 lhs, Vector4 rhs) => !(lhs == rhs);

    public override int GetHashCode() => HashUtil.Of(X, Y, Z, W);

    public bool Equals(Vector4 other) =>
        X == other.X && Y == other.Y && Z == other.Z && W == other.W;

    public override bool Equals(object? obj) => obj is Vector4 v && Equals(v);

    public override string ToString() => ToString(null, null);

    public string ToString(string format) => ToString(format, null);

    public string ToString(string? format, IFormatProvider? formatProvider) =>
        FormatUtil.Format("F2", format, formatProvider, X, Y, Z, W);
}
