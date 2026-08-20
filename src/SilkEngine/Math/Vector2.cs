using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SilkEngine.Math;

/// <summary>
/// 二维向量（左手系坐标约定，行主序存储）。
/// Sequential 布局保证 2 个 float 连续，供渲染层 fixed 指针零分配上传（GL 上传 transpose=true 无需转置）。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Vector2 : IEquatable<Vector2>
{
    /// <summary>X 分量。</summary>
    public float X;

    /// <summary>Y 分量。</summary>
    public float Y;

    /// <summary>以指定分量构造向量。</summary>
    /// <param name="x">X 分量。</param>
    /// <param name="y">Y 分量。</param>
    public Vector2(float x, float y)
    {
        X = x;
        Y = y;
    }

    /// <summary>向量长度（欧几里得范数）。</summary>
    public float Magnitude => MathF.Sqrt(X * X + Y * Y);

    /// <summary>向量长度平方（避免开方的快速比较用）。</summary>
    public float MagnitudeSquared => X * X + Y * Y;

    /// <summary>零向量 (0, 0)。</summary>
    public static readonly Vector2 Zero = new(0, 0);

    /// <summary>向量加法（分量相加）。</summary>
    /// <param name="a">被加向量。</param>
    /// <param name="b">加向量。</param>
    /// <returns>a + b。</returns>
    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);

    /// <summary>向量减法（分量相减）。</summary>
    /// <param name="a">被减向量。</param>
    /// <param name="b">减向量。</param>
    /// <returns>a - b。</returns>
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);

    /// <summary>向量取反（各分量变号）。</summary>
    /// <param name="v">原向量。</param>
    /// <returns>-v。</returns>
    public static Vector2 operator -(Vector2 v) => new(-v.X, -v.Y);

    /// <summary>向量数乘（各分量乘标量）。</summary>
    /// <param name="v">原向量。</param>
    /// <param name="s">标量。</param>
    /// <returns>v * s。</returns>
    public static Vector2 operator *(Vector2 v, float s) => new(v.X * s, v.Y * s);

    /// <summary>标量乘向量（交换律，等价于 v * s）。</summary>
    /// <param name="s">标量。</param>
    /// <param name="v">原向量。</param>
    /// <returns>s * v。</returns>
    public static Vector2 operator *(float s, Vector2 v) => v * s;

    /// <summary>点积（内积）。</summary>
    /// <param name="a">第一向量。</param>
    /// <param name="b">第二向量。</param>
    /// <returns>a·b。</returns>
    public static float Dot(Vector2 a, Vector2 b) => a.X * b.X + a.Y * b.Y;

    /// <summary>两点间欧几里得距离。</summary>
    /// <param name="a">起点。</param>
    /// <param name="b">终点。</param>
    /// <returns>(b - a) 的长度。</returns>
    public static float Distance(Vector2 a, Vector2 b) => (a - b).Magnitude;

    /// <summary>线性插值（t 越界时不钳制，允许外推）。</summary>
    /// <param name="a">起点。</param>
    /// <param name="b">终点。</param>
    /// <param name="t">插值参数（0 返回 a，1 返回 b）。</param>
    /// <returns>a + (b - a) * t。</returns>
    public static Vector2 Lerp(Vector2 a, Vector2 b, float t) => a + (b - a) * t;

    /// <summary>分量全等比较。</summary>
    /// <param name="a">第一向量。</param>
    /// <param name="b">第二向量。</param>
    /// <returns>各分量完全相等时为 true。</returns>
    public static bool operator ==(Vector2 a, Vector2 b) => a.X == b.X && a.Y == b.Y;

    /// <summary>分量不等比较。</summary>
    /// <param name="a">第一向量。</param>
    /// <param name="b">第二向量。</param>
    /// <returns>任一分量不等时为 true。</returns>
    public static bool operator !=(Vector2 a, Vector2 b) => !(a == b);

    /// <summary>与另一 Vector2 相等比较。</summary>
    /// <param name="other">比较对象。</param>
    /// <returns>分量完全相等时为 true。</returns>
    public bool Equals(Vector2 other) => this == other;

    /// <summary>与任意对象相等比较（类型为 Vector2 时按分量比较）。</summary>
    /// <param name="obj">比较对象。</param>
    /// <returns>obj 为 Vector2 且分量相等时为 true。</returns>
    public override bool Equals(object? obj) => obj is Vector2 v && Equals(v);

    /// <summary>基于分量的哈希码。</summary>
    /// <returns>HashCode.Combine(X, Y)。</returns>
    public override int GetHashCode() => HashCode.Combine(X, Y);

    /// <summary>格式化为 "(X, Y)" 字符串。</summary>
    /// <returns>形如 "(1, 2)" 的字符串。</returns>
    public override string ToString() => $"({X}, {Y})";

    /// <summary>隐式转换为 System.Numerics.Vector2（零拷贝 reinterpret）。</summary>
    /// <param name="v">引擎向量。</param>
    /// <returns>布局一致的 System.Numerics.Vector2。</returns>
    public static implicit operator System.Numerics.Vector2(Vector2 v) =>
        Unsafe.As<Vector2, System.Numerics.Vector2>(ref v);

    /// <summary>从 System.Numerics.Vector2 隐式转换（零拷贝 reinterpret）。</summary>
    /// <param name="v">System.Numerics 向量。</param>
    /// <returns>布局一致的引擎 Vector2。</returns>
    public static implicit operator Vector2(System.Numerics.Vector2 v) =>
        Unsafe.As<System.Numerics.Vector2, Vector2>(ref v);
}
