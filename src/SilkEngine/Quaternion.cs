using SilkEngine.Mathematics;
using SilkEngine.Mathematics.Internal;

namespace SilkEngine;

/// <summary>
/// A unit quaternion used for representing 3D rotations.
/// Axis convention matches Unreal: +X Forward, +Y Right, +Z Up.
/// </summary>
/// <remarks>
/// Pure value type: <c>readonly struct</c> with <c>public readonly</c> components, no in-place
/// mutators. <c>operator ==</c>, <see cref="Equals(Quaternion)"/> and <see cref="GetHashCode"/> are
/// exact and mutually consistent; use <see cref="Approximately(in Quaternion, in Quaternion)"/> when
/// a tolerance is intended. <c>q</c> and <c>-q</c> are different values under that contract even
/// though they represent the same rotation.
/// </remarks>
[MathValue("Quaternion", "F3", "X", "Y", "Z", "W")]
public readonly partial struct Quaternion : IEquatable<Quaternion>, IFormattable
{
    public readonly float X,
        Y,
        Z,
        W;

    public Quaternion(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    /// <summary>
    /// The identity rotation (no rotation).
    /// <br/>(0, 0, 0, 1f)
    /// </summary>
    public static readonly Quaternion Identity = new(0f, 0f, 0f, 1f);

    public float magnitude => EngineMath.Sqrt(sqrMagnitude);
    public float sqrMagnitude => X * X + Y * Y + Z * Z + W * W;
    public Quaternion normalized => Normalize(this);

    /// <summary>Returns a normalized copy of the given quaternion.</summary>
    /// <param name="q">Quaternion to normalize.</param>
    /// <remarks>Returns <see cref="Identity"/> when the magnitude is below <see cref="EngineMath.kEpsilon"/>.</remarks>
    public static Quaternion Normalize(Quaternion q)
    {
        float mag = q.magnitude;
        if (mag < EngineMath.kEpsilon)
            return Identity;
        float inv = 1f / mag;
        return new Quaternion(q.X * inv, q.Y * inv, q.Z * inv, q.W * inv);
    }

    /// <summary>Returns the conjugate of this quaternion.</summary>
    public Quaternion Conjugate() => new(-X, -Y, -Z, W);

    /// <summary>Returns the inverse of the given quaternion.</summary>
    public static Quaternion Inverse(Quaternion q)
    {
        float sq = q.sqrMagnitude;
        if (sq < EngineMath.kEpsilon)
            return Identity;
        float inv = 1f / sq;
        return new Quaternion(-q.X * inv, -q.Y * inv, -q.Z * inv, q.W * inv);
    }

    /// <summary>Dot product of two quaternions.</summary>
    public static float Dot(Quaternion a, Quaternion b) =>
        a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W;

    /// <summary>Angle in degrees between two rotations (0..180).</summary>
    public static float Angle(Quaternion a, Quaternion b)
    {
        float dot = EngineMath.Min(EngineMath.Abs(Dot(a, b)), 1f);
        return dot > 0.999999f ? 0f : EngineMath.Acos(dot) * 2f * EngineMath.Rad2Deg;
    }

    /// <summary>Creates a rotation which rotates angle degrees around axis.</summary>
    public static Quaternion AngleAxis(float angleDeg, Vector3 axis)
    {
        axis = Vector3.Normalize(axis);
        float halfRad = angleDeg * EngineMath.Deg2Rad * 0.5f;
        float s = EngineMath.Sin(halfRad);
        return new Quaternion(axis.X * s, axis.Y * s, axis.Z * s, EngineMath.Cos(halfRad));
    }

    /// <summary>Decomposes this rotation into (angle in degrees, unit axis).</summary>
    public void ToAngleAxis(out float angleDeg, out Vector3 axis)
    {
        Quaternion q = normalized;
        angleDeg = 2f * EngineMath.Acos(EngineMath.Max(EngineMath.Min(q.W, 1f), -1f)) * EngineMath.Rad2Deg;
        float s = EngineMath.Sqrt(EngineMath.Max(1f - q.W * q.W, 0f));
        axis = s < EngineMath.kEpsilon ? Vector3.Up : new Vector3(q.X / s, q.Y / s, q.Z / s);
    }

    /// <summary>Creates a rotation from Euler angles, expressed as a <see cref="Rotator"/>.</summary>
    /// <param name="rotator">Pitch/yaw/roll in degrees (Unreal sign convention).</param>
    /// <returns>The equivalent rotation.</returns>
    /// <remarks>Wraps <see cref="Rotator.ToQuaternion"/>; keeps the single conversion chain.</remarks>
    public static Quaternion FromRotator(in Rotator rotator) => rotator.ToQuaternion();

    /// <summary>Returns this rotation as Euler angles (pitch/yaw/roll in degrees).</summary>
    /// <returns>The equivalent <see cref="Rotator"/>.</returns>
    public Rotator ToRotator() => Rotator.FromQuaternion(this);

    /// <summary>Builds a row-vector rotation matrix (v * M == q * v).</summary>
    /// <returns>The rotation matrix.</returns>
    /// <remarks>Inverse of <see cref="FromMatrix"/>.</remarks>
    public Float4x4 ToMatrix()
    {
        float x = X,
            y = Y,
            z = Z,
            w = W;
        float xx = x * 2f * x,
            yy = y * 2f * y,
            zz = z * 2f * z;
        float xy = x * 2f * y,
            xz = x * 2f * z,
            yz = y * 2f * z;
        float wx = w * 2f * x,
            wy = w * 2f * y,
            wz = w * 2f * z;

        return new Float4x4(
            1f - (yy + zz),
            xy + wz,
            xz - wy,
            0f,
            xy - wz,
            1f - (xx + zz),
            yz + wx,
            0f,
            xz + wy,
            yz - wx,
            1f - (xx + yy),
            0f,
            0f,
            0f,
            0f,
            1f
        );
    }

    /// <summary>
    /// Creates a rotation looking along <paramref name="forward"/> with the given up direction.
    /// Uses Unreal-style axes (+X Forward, +Z Up).
    /// </summary>
    public static Quaternion LookRotation(Vector3 forward, Vector3 upwards)
    {
        Vector3 x = Vector3.Normalize(forward);
        Vector3 z = Vector3.Normalize(upwards);

        Vector3 y = Vector3.Cross(z, x);
        if (y.sqrMagnitude < EngineMath.kEpsilonNormalSqrt)
        {
            Vector3 helper = EngineMath.Abs(x.Z) < 0.9f ? Vector3.Up : Vector3.Right;
            y = Vector3.Cross(Vector3.Normalize(helper), x);
        }
        y = Vector3.Normalize(y);
        z = Vector3.Cross(x, y);

        Float4x4 m = new Float4x4(
            x.X,
            x.Y,
            x.Z,
            0,
            y.X,
            y.Y,
            y.Z,
            0,
            z.X,
            z.Y,
            z.Z,
            0,
            0,
            0,
            0,
            1
        );
        return FromMatrix(m);
    }

    /// <summary>Same as <see cref="LookRotation(Vector3, Vector3)"/> but with Up = +Z.</summary>
    public static Quaternion LookRotation(Vector3 forward) => LookRotation(forward, Vector3.Up);

    public static Quaternion FromToRotation(Vector3 from, Vector3 to)
    {
        Vector3 a = Vector3.Normalize(from);
        Vector3 b = Vector3.Normalize(to);
        float dot = EngineMath.Clamp(Vector3.Dot(a, b), -1f, 1f);

        if (dot > 1f - 1e-6f)
            return Identity;

        if (dot < -1f + 1e-6f)
        {
            Vector3 helper = EngineMath.Abs(a.X) < 0.9f ? Vector3.Up : Vector3.Right;
            Vector3 axis = Vector3.Normalize(Vector3.Cross(a, helper));
            return AngleAxis(180f, axis);
        }

        Vector3 c = Vector3.Cross(a, b);
        return Normalize(new Quaternion(c.X, c.Y, c.Z, 1f + dot));
    }

    /// <summary>
    /// Extracts a quaternion from a row-vector matrix (as produced by <see cref="ToMatrix"/>),
    /// where v * M == q * v.
    /// </summary>
    public static Quaternion FromMatrix(in Float4x4 m)
    {
        float trace = m.M00 + m.M11 + m.M22;

        if (trace > 0f)
        {
            float s = EngineMath.Sqrt(trace + 1f) * 2f;
            float invS = 1f / s;
            return new Quaternion(
                (m.M12 - m.M21) * invS,
                (m.M20 - m.M02) * invS,
                (m.M01 - m.M10) * invS,
                0.25f * s
            );
        }

        if (m.M00 > m.M11 && m.M00 > m.M22)
        {
            float s = EngineMath.Sqrt(1f + m.M00 - m.M11 - m.M22) * 2f;
            float invS = 1f / s;
            return new Quaternion(
                0.25f * s,
                (m.M10 + m.M01) * invS,
                (m.M20 + m.M02) * invS,
                (m.M12 - m.M21) * invS
            );
        }

        if (m.M11 > m.M22)
        {
            float s = EngineMath.Sqrt(1f + m.M11 - m.M00 - m.M22) * 2f;
            float invS = 1f / s;
            return new Quaternion(
                (m.M10 + m.M01) * invS,
                0.25f * s,
                (m.M21 + m.M12) * invS,
                (m.M20 - m.M02) * invS
            );
        }

        {
            float s = EngineMath.Sqrt(1f + m.M22 - m.M00 - m.M11) * 2f;
            float invS = 1f / s;
            return new Quaternion(
                (m.M20 + m.M02) * invS,
                (m.M21 + m.M12) * invS,
                0.25f * s,
                (m.M01 - m.M10) * invS
            );
        }
    }

    public static Quaternion Lerp(Quaternion a, Quaternion b, float t)
    {
        t = EngineMath.Clamp01(t);
        return LerpUnclamped(a, b, t);
    }

    public static Quaternion LerpUnclamped(Quaternion a, Quaternion b, float t)
    {
        if (Dot(a, b) < 0f)
            b = new Quaternion(-b.X, -b.Y, -b.Z, -b.W);
        return Normalize(
            new Quaternion(
                a.X + (b.X - a.X) * t,
                a.Y + (b.Y - a.Y) * t,
                a.Z + (b.Z - a.Z) * t,
                a.W + (b.W - a.W) * t
            )
        );
    }

    public static Quaternion Slerp(Quaternion a, Quaternion b, float t)
    {
        t = EngineMath.Clamp01(t);
        return SlerpUnclamped(a, b, t);
    }

    public static Quaternion SlerpUnclamped(Quaternion a, Quaternion b, float t)
    {
        float dot = Dot(a, b);
        if (dot < 0f)
        {
            b = new Quaternion(-b.X, -b.Y, -b.Z, -b.W);
            dot = -dot;
        }
        if (dot > 0.9995f)
            return Normalize(LerpUnclamped(a, b, t));

        dot = EngineMath.Clamp(dot, -1f, 1f);
        float theta = EngineMath.Acos(dot);
        float sinTheta = EngineMath.Sin(theta);
        float t1 = EngineMath.Sin((1f - t) * theta) / sinTheta;
        float t2 = EngineMath.Sin(t * theta) / sinTheta;

        return new Quaternion(
            a.X * t1 + b.X * t2,
            a.Y * t1 + b.Y * t2,
            a.Z * t1 + b.Z * t2,
            a.W * t1 + b.W * t2
        );
    }

    public static Quaternion RotateTowards(Quaternion from, Quaternion to, float maxDegreesDelta)
    {
        float angle = Angle(from, to);
        if (angle == 0f)
            return to;
        return SlerpUnclamped(from, to, EngineMath.Min(1f, maxDegreesDelta / angle));
    }

    /// <summary>Quaternion multiplication (composes rotations, applies b first).</summary>
    public static Quaternion operator *(Quaternion a, Quaternion b) =>
        new(
            a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
            a.W * b.Y + a.Y * b.W + a.Z * b.X - a.X * b.Z,
            a.W * b.Z + a.Z * b.W + a.X * b.Y - a.Y * b.X,
            a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z
        );

    /// <summary>Rotates a point by this quaternion (v' = q * v * q^-1).</summary>
    public static Vector3 operator *(Quaternion rotation, Vector3 point)
    {
        float x = rotation.X * 2f,
            y = rotation.Y * 2f,
            z = rotation.Z * 2f;
        float xx = rotation.X * x,
            yy = rotation.Y * y,
            zz = rotation.Z * z;
        float xy = rotation.X * y,
            xz = rotation.X * z,
            yz = rotation.Y * z;
        float wx = rotation.W * x,
            wy = rotation.W * y,
            wz = rotation.W * z;

        return new Vector3(
            (1f - (yy + zz)) * point.X + (xy - wz) * point.Y + (xz + wy) * point.Z,
            (xy + wz) * point.X + (1f - (xx + zz)) * point.Y + (yz - wx) * point.Z,
            (xz - wy) * point.X + (yz + wx) * point.Y + (1f - (xx + yy)) * point.Z
        );
    }

    /// <summary>
    /// Approximate equality with the shared relative-tolerance policy
    /// (see <see cref="EngineMath.Approximately(float, float)"/>).
    /// </summary>
    public static bool Approximately(in Quaternion a, in Quaternion b) =>
        Approx.Equal(a.X, a.Y, a.Z, a.W, b.X, b.Y, b.Z, b.W);

}
