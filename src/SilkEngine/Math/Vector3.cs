using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SilkEngine.Math;

[StructLayout(LayoutKind.Sequential)]
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

    public float Magnitude => MathF.Sqrt(X * X + Y * Y + Z * Z);

    public Vector3 Normalized =>
        Magnitude > Mathf.Epsilon ? new(X / Magnitude, Y / Magnitude, Z / Magnitude) : Zero;

    public static readonly Vector3 Zero = new(0, 0, 0);
    public static readonly Vector3 One = new(1, 1, 1);
    public static readonly Vector3 Up = new(0, 1, 0);
    public static readonly Vector3 Down = new(0, -1, 0);
    public static readonly Vector3 Left = new(-1, 0, 0);
    public static readonly Vector3 Right = new(1, 0, 0);
    public static readonly Vector3 Forward = new(0, 0, 1);
    public static readonly Vector3 Back = new(0, 0, -1);

    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Vector3 operator -(Vector3 v) => new(-v.X, -v.Y, -v.Z);

    public static Vector3 operator *(Vector3 v, float s) => new(v.X * s, v.Y * s, v.Z * s);

    public static Vector3 operator *(float s, Vector3 v) => v * s;

    public static float Dot(Vector3 a, Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    public static Vector3 Cross(Vector3 a, Vector3 b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    public static float Distance(Vector3 a, Vector3 b) => (a - b).Magnitude;

    public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a + (b - a) * t;

    public static bool operator ==(Vector3 a, Vector3 b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;

    public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);

    public bool Equals(Vector3 other) => this == other;

    public override bool Equals(object? obj) => obj is Vector3 v && Equals(v);

    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    public override string ToString() => $"({X}, {Y}, {Z})";

    public static implicit operator System.Numerics.Vector3(Vector3 v) =>
        Unsafe.As<Vector3, System.Numerics.Vector3>(ref v);

    public static implicit operator Vector3(System.Numerics.Vector3 v) =>
        Unsafe.As<System.Numerics.Vector3, Vector3>(ref v);
}
