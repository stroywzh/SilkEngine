using System;
using System.Runtime.InteropServices;

namespace SilkEngine.Math;

/// <summary>
/// 左手系 4x4 矩阵（行主序存储）；投影深度约定 GL NDC [-1,1]，相机前方为 +Z（CreateLookAt 约定）。
/// Sequential 布局保证 16 个 float 连续，供渲染层 fixed 指针零分配上传；GL 上传 transpose=true（UniformMatrix4 列主序约定）。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Matrix4x4 : IEquatable<Matrix4x4>
{
    /// <summary>第 1 行第 1 列元素。</summary>
    public float M11;

    /// <summary>第 1 行第 2 列元素。</summary>
    public float M12;

    /// <summary>第 1 行第 3 列元素。</summary>
    public float M13;

    /// <summary>第 1 行第 4 列元素。</summary>
    public float M14;

    /// <summary>第 2 行第 1 列元素。</summary>
    public float M21;

    /// <summary>第 2 行第 2 列元素。</summary>
    public float M22;

    /// <summary>第 2 行第 3 列元素。</summary>
    public float M23;

    /// <summary>第 2 行第 4 列元素。</summary>
    public float M24;

    /// <summary>第 3 行第 1 列元素。</summary>
    public float M31;

    /// <summary>第 3 行第 2 列元素。</summary>
    public float M32;

    /// <summary>第 3 行第 3 列元素。</summary>
    public float M33;

    /// <summary>第 3 行第 4 列元素。</summary>
    public float M34;

    /// <summary>第 4 行第 1 列元素。</summary>
    public float M41;

    /// <summary>第 4 行第 2 列元素。</summary>
    public float M42;

    /// <summary>第 4 行第 3 列元素。</summary>
    public float M43;

    /// <summary>第 4 行第 4 列元素。</summary>
    public float M44;

    /// <summary>单位矩阵（主对角线为 1，其余为 0）。</summary>
    public static readonly Matrix4x4 Identity = new()
    {
        M11 = 1,
        M22 = 1,
        M33 = 1,
        M44 = 1,
    };

    /// <summary>3×3 余子式行列式（行主序参数展开）。</summary>
    private static float Det3(
        float a,
        float b,
        float c,
        float d,
        float e,
        float f,
        float g,
        float h,
        float i
    ) => a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);

    /// <summary>4×4 行列式（第一行余子式展开）。</summary>
    /// <returns>矩阵行列式。</returns>
    public float Determinant =>
        M11 * Det3(M22, M23, M24, M32, M33, M34, M42, M43, M44)
        - M12 * Det3(M21, M23, M24, M31, M33, M34, M41, M43, M44)
        + M13 * Det3(M21, M22, M24, M31, M32, M34, M41, M42, M44)
        - M14 * Det3(M21, M22, M23, M31, M32, M33, M41, M42, M43);

    /// <summary>
    /// 逆矩阵（伴随矩阵除以行列式）。当 |det| &lt; 1e-12 视为奇异矩阵，
    /// 抛出 <see cref="InvalidOperationException"/>。
    /// </summary>
    /// <returns>满足 m · m⁻¹ = Identity 的矩阵。</returns>
    /// <exception cref="InvalidOperationException">矩阵不可逆（|det| &lt; 1e-12）。</exception>
    public Matrix4x4 Inverse
    {
        get
        {
            float det = Determinant;
            if (MathF.Abs(det) < 1e-12f)
                throw new InvalidOperationException("矩阵不可逆");
            float invDet = 1f / det;
            return new Matrix4x4
            {
                M11 = Det3(M22, M23, M24, M32, M33, M34, M42, M43, M44) * invDet,
                M12 = -Det3(M12, M13, M14, M32, M33, M34, M42, M43, M44) * invDet,
                M13 = Det3(M12, M13, M14, M22, M23, M24, M42, M43, M44) * invDet,
                M14 = -Det3(M12, M13, M14, M22, M23, M24, M32, M33, M34) * invDet,
                M21 = -Det3(M21, M23, M24, M31, M33, M34, M41, M43, M44) * invDet,
                M22 = Det3(M11, M13, M14, M31, M33, M34, M41, M43, M44) * invDet,
                M23 = -Det3(M11, M13, M14, M21, M23, M24, M41, M43, M44) * invDet,
                M24 = Det3(M11, M13, M14, M21, M23, M24, M31, M33, M34) * invDet,
                M31 = Det3(M21, M22, M24, M31, M32, M34, M41, M42, M44) * invDet,
                M32 = -Det3(M11, M12, M14, M31, M32, M34, M41, M42, M44) * invDet,
                M33 = Det3(M11, M12, M14, M21, M22, M24, M41, M42, M44) * invDet,
                M34 = -Det3(M11, M12, M14, M21, M22, M24, M31, M32, M34) * invDet,
                M41 = -Det3(M21, M22, M23, M31, M32, M33, M41, M42, M43) * invDet,
                M42 = Det3(M11, M12, M13, M31, M32, M33, M41, M42, M43) * invDet,
                M43 = -Det3(M11, M12, M13, M21, M22, M23, M41, M42, M43) * invDet,
                M44 = Det3(M11, M12, M13, M21, M22, M23, M31, M32, M33) * invDet,
            };
        }
    }

    /// <summary>转置矩阵（行列互换）。</summary>
    /// <returns>当前矩阵的转置。</returns>
    public Matrix4x4 Transposed =>
        new()
        {
            M11 = M11,
            M12 = M21,
            M13 = M31,
            M14 = M41,
            M21 = M12,
            M22 = M22,
            M23 = M32,
            M24 = M42,
            M31 = M13,
            M32 = M23,
            M33 = M33,
            M34 = M43,
            M41 = M14,
            M42 = M24,
            M43 = M34,
            M44 = M44,
        };

    /// <summary>创建平移矩阵（行主序，平移量位于第 4 列）。</summary>
    /// <param name="pos">平移向量。</param>
    /// <returns>平移矩阵。</returns>
    public static Matrix4x4 CreateTranslation(Vector3 pos) =>
        new()
        {
            M11 = 1,
            M22 = 1,
            M33 = 1,
            M44 = 1,
            M14 = pos.X,
            M24 = pos.Y,
            M34 = pos.Z,
        };

    /// <summary>创建缩放矩阵（主对角线为缩放分量）。</summary>
    /// <param name="scale">各轴缩放分量。</param>
    /// <returns>缩放矩阵。</returns>
    public static Matrix4x4 CreateScale(Vector3 scale) =>
        new()
        {
            M11 = scale.X,
            M22 = scale.Y,
            M33 = scale.Z,
            M44 = 1,
        };

    /// <summary>从四元数创建 3x3 旋转矩阵（行主序，左上 3×3 区域）。</summary>
    /// <param name="q">旋转四元数。</param>
    /// <returns>旋转矩阵。</returns>
    public static Matrix4x4 CreateRotation(Quaternion q)
    {
        float xx = q.X * q.X,
            yy = q.Y * q.Y,
            zz = q.Z * q.Z;
        float xy = q.X * q.Y,
            xz = q.X * q.Z,
            yz = q.Y * q.Z;
        float wx = q.W * q.X,
            wy = q.W * q.Y,
            wz = q.W * q.Z;
        return new Matrix4x4
        {
            M11 = 1 - 2 * (yy + zz),
            M12 = 2 * (xy + wz),
            M13 = 2 * (xz - wy),
            M21 = 2 * (xy - wz),
            M22 = 1 - 2 * (xx + zz),
            M23 = 2 * (yz + wx),
            M31 = 2 * (xz + wy),
            M32 = 2 * (yz - wx),
            M33 = 1 - 2 * (xx + yy),
            M44 = 1,
        };
    }

    /// <summary>
    /// 创建透视投影矩阵（左手 GL NDC [-1,1] 深度约定，行主序存储）。
    /// 引擎左手系：相机前方为 +Z（CreateLookAt 约定），视空间 z=+near 映射到 NDC -1，
    /// z=+far 映射到 NDC +1，clip.w = z（前方几何 w&gt;0 可见）。
    /// </summary>
    /// <param name="fov">垂直视场角（弧度），须满足 0 &lt; fov &lt; π。</param>
    /// <param name="aspect">宽高比（宽/高），须为正。</param>
    /// <param name="near">近裁剪面距离，须为正且小于 far。</param>
    /// <param name="far">远裁剪面距离，须为正且大于 near。</param>
    /// <returns>透视投影矩阵。</returns>
    /// <exception cref="ArgumentOutOfRangeException">fov/aspect 越界或 near/far 非法（非正、far ≤ near、非有限值）。</exception>
    public static Matrix4x4 CreatePerspectiveFieldOfView(
        float fov,
        float aspect,
        float near,
        float far
    )
    {
        if (
            !float.IsFinite(fov)
            || fov <= 0f
            || fov >= MathF.PI
            || !float.IsFinite(aspect)
            || aspect <= 0f
        )
            throw new ArgumentOutOfRangeException(nameof(fov), "fov 须为 (0, π) 内的有限值，aspect 须为正有限值");
        if (!float.IsFinite(near) || near <= 0f || !float.IsFinite(far) || far <= near)
            throw new ArgumentOutOfRangeException(nameof(near), "near/far 须为正有限值且 far > near");

        float f = 1f / MathF.Tan(fov * 0.5f);
        return new Matrix4x4
        {
            M11 = f / aspect,
            M22 = f,
            M33 = (far + near) / (far - near),
            M34 = -2f * near * far / (far - near),
            M43 = 1f,
            M44 = 0f,
        };
    }

    /// <summary>
    /// 创建正交投影矩阵（左手 GL NDC [-1,1] 深度约定，行主序存储）。
    /// 引擎左手系：相机前方为 +Z（CreateLookAt 约定），视空间 z=+near 映射到 NDC -1，
    /// z=+far 映射到 NDC +1，clip.w = 1。
    /// </summary>
    /// <param name="width">视锥宽度，须为正。</param>
    /// <param name="height">视锥高度，须为正。</param>
    /// <param name="near">近裁剪面距离，须为正且小于 far。</param>
    /// <param name="far">远裁剪面距离，须为正且大于 near。</param>
    /// <returns>正交投影矩阵。</returns>
    /// <exception cref="ArgumentOutOfRangeException">width/height 非正或 near/far 非法（非正、far ≤ near、非有限值）。</exception>
    public static Matrix4x4 CreateOrthographic(float width, float height, float near, float far)
    {
        if (
            !float.IsFinite(width)
            || width <= 0f
            || !float.IsFinite(height)
            || height <= 0f
        )
            throw new ArgumentOutOfRangeException(nameof(width), "width/height 须为正有限值");
        if (!float.IsFinite(near) || near <= 0f || !float.IsFinite(far) || far <= near)
            throw new ArgumentOutOfRangeException(nameof(near), "near/far 须为正有限值且 far > near");

        float r = 1f / (far - near);
        return new Matrix4x4
        {
            M11 = 2f / width,
            M22 = 2f / height,
            M33 = 2f * r,
            M34 = -(far + near) * r,
            M43 = 0f,
            M44 = 1f,
        };
    }

    /// <summary>创建 TRS 复合矩阵：T · R · S（先缩放、再旋转、后平移）。</summary>
    /// <param name="pos">平移向量。</param>
    /// <param name="rot">旋转四元数。</param>
    /// <param name="scale">各轴缩放分量。</param>
    /// <returns>复合变换矩阵。</returns>
    public static Matrix4x4 CreateTRS(Vector3 pos, Quaternion rot, Vector3 scale) =>
        CreateTranslation(pos) * CreateRotation(rot) * CreateScale(scale);

    /// <summary>组合 MVP 矩阵（GL 列主序上传约定）：projection * view * model</summary>
    /// <param name="projection">投影矩阵。</param>
    /// <param name="view">视图矩阵。</param>
    /// <param name="model">模型矩阵。</param>
    /// <returns>projection * view * model。</returns>
    public static Matrix4x4 ComposeMVP(
        Matrix4x4 projection,
        Matrix4x4 view,
        Matrix4x4 model
    ) => projection * view * model;

    /// <summary>
    /// 创建视图矩阵（LookAt，左手系）。
    /// 相机位于 eye 看向 target，+Z 为相机前方；right/up/fwd 基向量写入 3×3 旋转区，
    /// 平移列为负的基向量点乘 eye（行主序存储）。
    /// </summary>
    /// <param name="eye">相机位置。</param>
    /// <param name="target">注视目标点。</param>
    /// <param name="up">世界参考上方向（与视线不平行）。</param>
    /// <returns>视图矩阵。</returns>
    public static Matrix4x4 CreateLookAt(Vector3 eye, Vector3 target, Vector3 up)
    {
        Vector3 fwd = (target - eye).Normalized;
        Vector3 right = Vector3.Cross(up, fwd).Normalized;
        Vector3 u = Vector3.Cross(fwd, right);
        return new Matrix4x4
        {
            M11 = right.X,
            M12 = right.Y,
            M13 = right.Z,
            M21 = u.X,
            M22 = u.Y,
            M23 = u.Z,
            M31 = fwd.X,
            M32 = fwd.Y,
            M33 = fwd.Z,
            M14 = -Vector3.Dot(right, eye),
            M24 = -Vector3.Dot(u, eye),
            M34 = -Vector3.Dot(fwd, eye),
            M44 = 1,
        };
    }

    /// <summary>矩阵乘法（行主序：a · b，先应用 b 再应用 a 的列向量约定）。</summary>
    /// <param name="a">左矩阵。</param>
    /// <param name="b">右矩阵。</param>
    /// <returns>a · b。</returns>
    public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b) =>
        new()
        {
            M11 = a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31 + a.M14 * b.M41,
            M12 = a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32 + a.M14 * b.M42,
            M13 = a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33 + a.M14 * b.M43,
            M14 = a.M11 * b.M14 + a.M12 * b.M24 + a.M13 * b.M34 + a.M14 * b.M44,
            M21 = a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31 + a.M24 * b.M41,
            M22 = a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32 + a.M24 * b.M42,
            M23 = a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33 + a.M24 * b.M43,
            M24 = a.M21 * b.M14 + a.M22 * b.M24 + a.M23 * b.M34 + a.M24 * b.M44,
            M31 = a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31 + a.M34 * b.M41,
            M32 = a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32 + a.M34 * b.M42,
            M33 = a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33 + a.M34 * b.M43,
            M34 = a.M31 * b.M14 + a.M32 * b.M24 + a.M33 * b.M34 + a.M34 * b.M44,
            M41 = a.M41 * b.M11 + a.M42 * b.M21 + a.M43 * b.M31 + a.M44 * b.M41,
            M42 = a.M41 * b.M12 + a.M42 * b.M22 + a.M43 * b.M32 + a.M44 * b.M42,
            M43 = a.M41 * b.M13 + a.M42 * b.M23 + a.M43 * b.M33 + a.M44 * b.M43,
            M44 = a.M41 * b.M14 + a.M42 * b.M24 + a.M43 * b.M34 + a.M44 * b.M44,
        };

    /// <summary>矩阵变换向量（行向量 v · M，含透视除法 w = M₄₁x + M₄₂y + M₄₃z + M₄₄）。</summary>
    /// <param name="m">变换矩阵。</param>
    /// <param name="v">被变换向量（作为行向量左乘）。</param>
    /// <returns>v · M（齐次除法后）。</returns>
    public static Vector3 operator *(Matrix4x4 m, Vector3 v)
    {
        float w = m.M41 * v.X + m.M42 * v.Y + m.M43 * v.Z + m.M44;
        return new Vector3(
            (m.M11 * v.X + m.M12 * v.Y + m.M13 * v.Z + m.M14) / w,
            (m.M21 * v.X + m.M22 * v.Y + m.M23 * v.Z + m.M24) / w,
            (m.M31 * v.X + m.M32 * v.Y + m.M33 * v.Z + m.M34) / w
        );
    }

    /// <summary>元素全等比较。</summary>
    /// <param name="a">第一矩阵。</param>
    /// <param name="b">第二矩阵。</param>
    /// <returns>16 个元素完全相等时为 true。</returns>
    public static bool operator ==(Matrix4x4 a, Matrix4x4 b) =>
        a.M11 == b.M11
        && a.M12 == b.M12
        && a.M13 == b.M13
        && a.M14 == b.M14
        && a.M21 == b.M21
        && a.M22 == b.M22
        && a.M23 == b.M23
        && a.M24 == b.M24
        && a.M31 == b.M31
        && a.M32 == b.M32
        && a.M33 == b.M33
        && a.M34 == b.M34
        && a.M41 == b.M41
        && a.M42 == b.M42
        && a.M43 == b.M43
        && a.M44 == b.M44;

    /// <summary>元素不等比较。</summary>
    /// <param name="a">第一矩阵。</param>
    /// <param name="b">第二矩阵。</param>
    /// <returns>任一元不等时为 true。</returns>
    public static bool operator !=(Matrix4x4 a, Matrix4x4 b) => !(a == b);

    /// <summary>与另一 Matrix4x4 相等比较。</summary>
    /// <param name="other">比较对象。</param>
    /// <returns>元素完全相等时为 true。</returns>
    public bool Equals(Matrix4x4 other) => this == other;

    /// <summary>与任意对象相等比较（类型为 Matrix4x4 时按元素比较）。</summary>
    /// <param name="obj">比较对象。</param>
    /// <returns>obj 为 Matrix4x4 且元素相等时为 true。</returns>
    public override bool Equals(object? obj) => obj is Matrix4x4 m && Equals(m);

    /// <summary>基于元素的哈希码。</summary>
    /// <returns>全部 16 个元素的组合哈希。</returns>
    public override int GetHashCode() =>
        HashCode.Combine(
            HashCode.Combine(M11, M12, M13, M14, M21, M22, M23, M24),
            HashCode.Combine(M31, M32, M33, M34, M41, M42, M43, M44)
        );
}
