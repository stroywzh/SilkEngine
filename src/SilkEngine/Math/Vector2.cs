using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SilkEngine.Math;

[StructLayout(LayoutKind.Sequential)]
public struct Vector2 : IEquatable<Vector2>
{
    public float X,
        Y;

    public Vector2(float x, float y)
    {
        X = x;
        Y = y;
    }

    public static readonly Vector2 Zero = new(0, 0);

    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);

    public static bool operator ==(Vector2 a, Vector2 b) => a.X == b.X && a.Y == b.Y;

    public static bool operator !=(Vector2 a, Vector2 b) => !(a == b);

    public bool Equals(Vector2 other) => this == other;

    public override bool Equals(object? obj) => obj is Vector2 v && Equals(v);

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public override string ToString() => $"({X}, {Y})";

    public static implicit operator System.Numerics.Vector2(Vector2 v) =>
        Unsafe.As<Vector2, System.Numerics.Vector2>(ref v);

    public static implicit operator Vector2(System.Numerics.Vector2 v) =>
        Unsafe.As<System.Numerics.Vector2, Vector2>(ref v);
}
