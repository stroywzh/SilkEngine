using System.Globalization;

namespace SilkEngine;

public readonly partial struct Vector2Int : IEquatable<Vector2Int>, IFormattable
{
    public readonly int X,
        Y;

    public Vector2Int(int x, int y)
    {
        X = x;
        Y = y;
    }

    public Vector2Int(int value)
        : this(value, value) { }

    public static readonly Vector2Int Zero = new(0, 0);

    public static readonly Vector2Int One = new(1, 1);

    public static readonly Vector2Int UnitX = new(1, 0);

    public static readonly Vector2Int UnitY = new(0, 1);

    public int sqrMagnitude => X * X + Y * Y;

    public float magnitude => EngineMath.Sqrt(sqrMagnitude);

    public static Vector2Int Scale(Vector2Int a, Vector2Int b) => new(a.X * b.X, a.Y * b.Y);

    public static Vector2Int Min(Vector2Int a, Vector2Int b) =>
        new(EngineMath.Min(a.X, b.X), EngineMath.Min(a.Y, b.Y));

    public static Vector2Int Max(Vector2Int a, Vector2Int b) =>
        new(EngineMath.Max(a.X, b.X), EngineMath.Max(a.Y, b.Y));

    public static float Distance(Vector2Int a, Vector2Int b) => (a - b).magnitude;

    public static Vector2Int operator +(Vector2Int a, Vector2Int b) => new(a.X + b.X, a.Y + b.Y);

    public static Vector2Int operator -(Vector2Int a, Vector2Int b) => new(a.X - b.X, a.Y - b.Y);

    public static Vector2Int operator -(Vector2Int a) => new(-a.X, -a.Y);

    public static Vector2Int operator *(Vector2Int a, int d) => new(a.X * d, a.Y * d);

    public static Vector2Int operator *(int d, Vector2Int a) => new(a.X * d, a.Y * d);

    public static Vector2Int operator /(Vector2Int a, int d) => new(a.X / d, a.Y / d);

    public static explicit operator Vector2(Vector2Int v) => new(v.X, v.Y);

    public static explicit operator Vector2Int(Vector2 v) => new((int)v.X, (int)v.Y);

    public static bool operator ==(Vector2Int lhs, Vector2Int rhs) => lhs.Equals(rhs);

    public static bool operator !=(Vector2Int lhs, Vector2Int rhs) => !(lhs == rhs);

    public bool Equals(Vector2Int other) => X == other.X && Y == other.Y;

    public override bool Equals(object? obj) => obj is Vector2Int other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return X.GetHashCode() * 397 ^ Y.GetHashCode();
        }
    }

    public int this[int index] =>
        index switch
        {
            0 => X,
            1 => Y,
            _ => throw new IndexOutOfRangeException(),
        };

    public override string ToString() => ToString(null, null);

    public string ToString(string? format) => ToString(format, null);

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        formatProvider ??= CultureInfo.InvariantCulture.NumberFormat;
        if (string.IsNullOrEmpty(format))
            format = "F0";

        return $"Vector2Int({X.ToString(format, formatProvider)}, {Y.ToString(format, formatProvider)})";
    }
}
