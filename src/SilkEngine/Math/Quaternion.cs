using System;
using System.Runtime.InteropServices;

namespace SilkEngine.Math;

/// <summary>
/// 四元数（左手系旋转，行主序存储）。
/// Sequential 布局保证 4 个 float 连续，供渲染层 fixed 指针零分配上传（GL 上传 transpose=true 约定与向量/矩阵一致）。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Quaternion : IEquatable<Quaternion>
{
    /// <summary>虚部 X 分量。</summary>
    public float X;

    /// <summary>虚部 Y 分量。</summary>
    public float Y;

    /// <summary>虚部 Z 分量。</summary>
    public float Z;

    /// <summary>实部 W 分量。</summary>
    public float W;

    /// <summary>以 x/y/z/w 分量构造四元数。</summary>
    /// <param name="x">虚部 X。</param>
    /// <param name="y">虚部 Y。</param>
    /// <param name="z">虚部 Z。</param>
    /// <param name="w">实部 W。</param>
    public Quaternion(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    /// <summary>单位四元数（零旋转，w=1）。</summary>
    public static readonly Quaternion Identity = new(0, 0, 0, 1);

    /// <summary>
    /// 按欧拉角构造旋转四元数；复合顺序为 pitch（绕 X）→ yaw（绕 Y）→ roll（绕 Z），
    /// 即 q = Rz(roll) · Ry(yaw) · Rx(pitch)。
    /// </summary>
    /// <param name="pitch">俯仰角（绕 X 轴，弧度）。</param>
    /// <param name="yaw">偏航角（绕 Y 轴，弧度）。</param>
    /// <param name="roll">翻滚角（绕 Z 轴，弧度）。</param>
    /// <returns>对应旋转的四元数。</returns>
    public static Quaternion Euler(float pitch, float yaw, float roll)
    {
        float halfX = pitch * MathF.PI / 360f;
        float halfY = yaw * MathF.PI / 360f;
        float halfZ = roll * MathF.PI / 360f;

        float cx = MathF.Cos(halfX),
            sx = MathF.Sin(halfX);
        float cy = MathF.Cos(halfY),
            sy = MathF.Sin(halfY);
        float cz = MathF.Cos(halfZ),
            sz = MathF.Sin(halfZ);

        return new Quaternion(
            sx * cy * cz - cx * sy * sz,
            cx * sy * cz + sx * cy * sz,
            cx * cy * sz - sx * sy * cz,
            cx * cy * cz + sx * sy * sz
        );
    }

    /// <summary>返回归一化四元数（各分量除以模）；模为零时返回 Identity。</summary>
    /// <returns>单位四元数，零模输入返回 Identity。</returns>
    public Quaternion Normalize()
    {
        var mag = MathF.Sqrt(W * W + X * X + Y * Y + Z * Z);
        return mag == 0f ? Identity : new Quaternion(X / mag, Y / mag, Z / mag, W / mag);
    }

    /// <summary>返回逆四元数（共轭除以模平方）；模为零时返回 Identity。</summary>
    /// <returns>满足 q * q⁻¹ = Identity 的四元数，零模输入返回 Identity。</returns>
    public Quaternion Inverse
    {
        get
        {
            var normSq = W * W + X * X + Y * Y + Z * Z;
            return normSq == 0f
                ? Identity
                : new Quaternion(-X / normSq, -Y / normSq, -Z / normSq, W / normSq);
        }
    }

    /// <summary>
    /// 四元数乘法（复合旋转，先 b 后 a：a * b 表示先应用 b 再应用 a）。
    /// 结果不保证为单位四元数，需要时可显式 <see cref="Normalize"/>。
    /// </summary>
    /// <param name="a">左四元数。</param>
    /// <param name="b">右四元数。</param>
    /// <returns>a * b（哈密顿积）。</returns>
    public static Quaternion operator *(Quaternion a, Quaternion b) =>
        new(
            a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
            a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
            a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
            a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z
        );

    /// <summary>旋转向量；非单位输入自动归一化（零模按 Identity 处理，即原样返回）。</summary>
    /// <param name="q">旋转四元数（可为非单位，内部归一化）。</param>
    /// <param name="v">被旋转向量。</param>
    /// <returns>v 绕 q 的轴旋转后的向量。</returns>
    public static Vector3 operator *(Quaternion q, Vector3 v)
    {
        Quaternion n = q.Normalize();
        Quaternion qv = new(v.X, v.Y, v.Z, 0);
        Quaternion conj = new(-n.X, -n.Y, -n.Z, n.W);
        Quaternion result = n * qv * conj;
        return new Vector3(result.X, result.Y, result.Z);
    }

    /// <summary>分量全等比较。</summary>
    /// <param name="a">第一四元数。</param>
    /// <param name="b">第二四元数。</param>
    /// <returns>各分量完全相等时为 true。</returns>
    public static bool operator ==(Quaternion a, Quaternion b) =>
        a.X == b.X && a.Y == b.Y && a.Z == b.Z && a.W == b.W;

    /// <summary>分量不等比较。</summary>
    /// <param name="a">第一四元数。</param>
    /// <param name="b">第二四元数。</param>
    /// <returns>任一分量不等时为 true。</returns>
    public static bool operator !=(Quaternion a, Quaternion b) => !(a == b);

    /// <summary>与另一 Quaternion 相等比较。</summary>
    /// <param name="other">比较对象。</param>
    /// <returns>分量完全相等时为 true。</returns>
    public bool Equals(Quaternion other) => this == other;

    /// <summary>与任意对象相等比较（类型为 Quaternion 时按分量比较）。</summary>
    /// <param name="obj">比较对象。</param>
    /// <returns>obj 为 Quaternion 且分量相等时为 true。</returns>
    public override bool Equals(object? obj) => obj is Quaternion q && Equals(q);

    /// <summary>基于分量的哈希码。</summary>
    /// <returns>HashCode.Combine(X, Y, Z, W)。</returns>
    public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);

    /// <summary>格式化为 "(X, Y, Z, W)" 字符串。</summary>
    /// <returns>形如 "(0, 0, 0, 1)" 的字符串。</returns>
    public override string ToString() => $"({X}, {Y}, {Z}, {W})";
}
