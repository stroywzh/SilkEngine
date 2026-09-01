using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SilkEngine.Math;

/// <summary>
/// 三维向量（左手系坐标约定，行主序存储；相机前方为 +Z，见 <see cref="Forward"/>）。
/// Sequential 布局保证 3 个 float 连续，供渲染层 fixed 指针零分配上传（GL 上传 transpose=true 无需转置）。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Vector3 : IEquatable<Vector3>
{
    /// <summary>X 分量。</summary>
    public float X;

    /// <summary>Y 分量。</summary>
    public float Y;

    /// <summary>Z 分量。</summary>
    public float Z;

    /// <summary>以指定分量构造向量。</summary>
    /// <param name="x">X 分量。</param>
    /// <param name="y">Y 分量。</param>
    /// <param name="z">Z 分量。</param>
    public Vector3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>向量长度（欧几里得范数）。</summary>
    public float Magnitude => MathF.Sqrt(X * X + Y * Y + Z * Z);

    /// <summary>归一化向量（各分量除以长度）；长度小于等于 Mathf.Epsilon 时返回 <see cref="Zero"/>。</summary>
    /// <returns>单位向量（长度 1），零向量输入返回 Zero。</returns>
    public Vector3 Normalized
    {
        get
        {
            float mag = Magnitude;
            return mag > Mathf.Epsilon ? new(X / mag, Y / mag, Z / mag) : Zero;
        }
    }

    /// <summary>零向量 (0, 0, 0)。</summary>
    public static readonly Vector3 Zero = new(0, 0, 0);

    /// <summary>单位向量 (1, 1, 1)。</summary>
    public static readonly Vector3 One = new(1, 1, 1);

    /// <summary>世界正上方向 (0, 1, 0)。</summary>
    public static readonly Vector3 Up = new(0, 1, 0);

    /// <summary>世界正右方向 (1, 0, 0)。</summary>
    public static readonly Vector3 Right = new(1, 0, 0);

    /// <summary>世界前方 (0, 0, 1)，与 CreateLookAt 相机前方约定一致（左手系 +Z）。</summary>
    public static readonly Vector3 Forward = new(0, 0, 1);

    /// <summary>向量加法（分量相加）。</summary>
    /// <param name="a">被加向量。</param>
    /// <param name="b">加向量。</param>
    /// <returns>a + b。</returns>
    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    /// <summary>向量减法（分量相减）。</summary>
    /// <param name="a">被减向量。</param>
    /// <param name="b">减向量。</param>
    /// <returns>a - b。</returns>
    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    /// <summary>向量取反（各分量变号）。</summary>
    /// <param name="v">原向量。</param>
    /// <returns>-v。</returns>
    public static Vector3 operator -(Vector3 v) => new(-v.X, -v.Y, -v.Z);

    /// <summary>向量数乘（各分量乘标量）。</summary>
    /// <param name="v">原向量。</param>
    /// <param name="s">标量。</param>
    /// <returns>v * s。</returns>
    public static Vector3 operator *(Vector3 v, float s) => new(v.X * s, v.Y * s, v.Z * s);

    /// <summary>标量乘向量（交换律，等价于 v * s）。</summary>
    /// <param name="s">标量。</param>
    /// <param name="v">原向量。</param>
    /// <returns>s * v。</returns>
    public static Vector3 operator *(float s, Vector3 v) => v * s;

    /// <summary>点积（内积）。</summary>
    /// <param name="a">第一向量。</param>
    /// <param name="b">第二向量。</param>
    /// <returns>a·b。</returns>
    public static float Dot(Vector3 a, Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    /// <summary>叉积（外积），结果垂直于两输入向量。</summary>
    /// <param name="a">第一向量。</param>
    /// <param name="b">第二向量。</param>
    /// <returns>a × b（左手系）。</returns>
    public static Vector3 Cross(Vector3 a, Vector3 b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    /// <summary>两点间欧几里得距离。</summary>
    /// <param name="a">起点。</param>
    /// <param name="b">终点。</param>
    /// <returns>(b - a) 的长度。</returns>
    public static float Distance(Vector3 a, Vector3 b) => (a - b).Magnitude;

    /// <summary>线性插值（t 越界时不钳制，允许外推）。</summary>
    /// <param name="a">起点。</param>
    /// <param name="b">终点。</param>
    /// <param name="t">插值参数（0 返回 a，1 返回 b）。</param>
    /// <returns>a + (b - a) * t。</returns>
    public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a + (b - a) * t;

    /// <summary>分量全等比较。</summary>
    /// <param name="a">第一向量。</param>
    /// <param name="b">第二向量。</param>
    /// <returns>各分量完全相等时为 true。</returns>
    public static bool operator ==(Vector3 a, Vector3 b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;

    /// <summary>分量不等比较。</summary>
    /// <param name="a">第一向量。</param>
    /// <param name="b">第二向量。</param>
    /// <returns>任一分量不等时为 true。</returns>
    public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);

    /// <summary>与另一 Vector3 相等比较。</summary>
    /// <param name="other">比较对象。</param>
    /// <returns>分量完全相等时为 true。</returns>
    public bool Equals(Vector3 other) => this == other;

    /// <summary>与任意对象相等比较（类型为 Vector3 时按分量比较）。</summary>
    /// <param name="obj">比较对象。</param>
    /// <returns>obj 为 Vector3 且分量相等时为 true。</returns>
    public override bool Equals(object? obj) => obj is Vector3 v && Equals(v);

    /// <summary>基于分量的哈希码。</summary>
    /// <returns>HashCode.Combine(X, Y, Z)。</returns>
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    /// <summary>格式化为 "(X, Y, Z)" 字符串。</summary>
    /// <returns>形如 "(1, 2, 3)" 的字符串。</returns>
    public override string ToString() => $"({X}, {Y}, {Z})";

    /// <summary>隐式转换为 System.Numerics.Vector3（零拷贝 reinterpret）。</summary>
    /// <param name="v">引擎向量。</param>
    /// <returns>布局一致的 System.Numerics.Vector3。</returns>
    public static implicit operator System.Numerics.Vector3(Vector3 v) =>
        Unsafe.As<Vector3, System.Numerics.Vector3>(ref v);

    /// <summary>从 System.Numerics.Vector3 隐式转换（零拷贝 reinterpret）。</summary>
    /// <param name="v">System.Numerics 向量。</param>
    /// <returns>布局一致的引擎 Vector3。</returns>
    public static implicit operator Vector3(System.Numerics.Vector3 v) =>
        Unsafe.As<System.Numerics.Vector3, Vector3>(ref v);
}
