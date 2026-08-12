using System;

namespace SilkEngine.Math;

public struct Quaternion : IEquatable<Quaternion>
{
    public float X, Y, Z, W;

    public Quaternion(float x, float y, float z, float w)
    {
        X = x; Y = y; Z = z; W = w;
    }

    public static readonly Quaternion Identity = new(0, 0, 0, 1);

    public static Quaternion Euler(float pitch, float yaw, float roll)
    {
        float halfX = pitch * MathF.PI / 360f;
        float halfY = yaw * MathF.PI / 360f;
        float halfZ = roll * MathF.PI / 360f;

        float cx = MathF.Cos(halfX), sx = MathF.Sin(halfX);
        float cy = MathF.Cos(halfY), sy = MathF.Sin(halfY);
        float cz = MathF.Cos(halfZ), sz = MathF.Sin(halfZ);

        return new Quaternion(
            sx * cy * cz - cx * sy * sz,
            cx * sy * cz + sx * cy * sz,
            cx * cy * sz - sx * sy * cz,
            cx * cy * cz + sx * sy * sz
        );
    }

    public static Quaternion operator *(Quaternion a, Quaternion b) =>
        new(
            a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
            a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
            a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
            a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z
        );

    public static Vector3 operator *(Quaternion q, Vector3 v)
    {
        Quaternion qv = new(v.X, v.Y, v.Z, 0);
        Quaternion conj = new(-q.X, -q.Y, -q.Z, q.W);
        Quaternion result = q * qv * conj;
        return new Vector3(result.X, result.Y, result.Z);
    }

    public static bool operator ==(Quaternion a, Quaternion b) =>
        a.X == b.X && a.Y == b.Y && a.Z == b.Z && a.W == b.W;
    public static bool operator !=(Quaternion a, Quaternion b) => !(a == b);

    public bool Equals(Quaternion other) => this == other;
    public override bool Equals(object? obj) => obj is Quaternion q && Equals(q);
    public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);
    public override string ToString() => $"({X}, {Y}, {Z}, {W})";
}
