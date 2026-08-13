using System;

namespace SilkEngine.Math;

public struct Matrix4x4 : IEquatable<Matrix4x4>
{
    public float M11,
        M12,
        M13,
        M14;
    public float M21,
        M22,
        M23,
        M24;
    public float M31,
        M32,
        M33,
        M34;
    public float M41,
        M42,
        M43,
        M44;

    public static Matrix4x4 Identity =>
        new()
        {
            M11 = 1,
            M22 = 1,
            M33 = 1,
            M44 = 1,
        };

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

    public static Matrix4x4 CreateScale(Vector3 scale) =>
        new()
        {
            M11 = scale.X,
            M22 = scale.Y,
            M33 = scale.Z,
            M44 = 1,
        };

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

    public static Matrix4x4 CreatePerspectiveFieldOfView(
        float fov,
        float aspect,
        float near,
        float far
    )
    {
        float f = 1f / MathF.Tan(fov * 0.5f);
        return new Matrix4x4
        {
            M11 = f / aspect,
            M22 = f,
            M33 = far / (far - near),
            M34 = -near * far / (far - near),
            M43 = 1f,
            M44 = 0f,
        };
    }

    public static Matrix4x4 CreateOrthographic(float width, float height, float near, float far)
    {
        float r = 1f / (far - near);
        return new Matrix4x4
        {
            M11 = 2f / width,
            M22 = 2f / height,
            M33 = r,
            M34 = -near * r,
            M43 = 0f,
            M44 = 1f,
        };
    }

    public static Matrix4x4 CreateTRS(Vector3 pos, Quaternion rot, Vector3 scale) =>
        CreateTranslation(pos) * CreateRotation(rot) * CreateScale(scale);

    /// <summary>组合 MVP 矩阵（GL 列主序上传约定）：projection * view * model</summary>
    public static Matrix4x4 ComposeMVP(
        Matrix4x4 projection,
        Matrix4x4 view,
        Matrix4x4 model
    ) => projection * view * model;

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

    public static Vector3 operator *(Matrix4x4 m, Vector3 v)
    {
        float w = m.M41 * v.X + m.M42 * v.Y + m.M43 * v.Z + m.M44;
        return new Vector3(
            (m.M11 * v.X + m.M12 * v.Y + m.M13 * v.Z + m.M14) / w,
            (m.M21 * v.X + m.M22 * v.Y + m.M23 * v.Z + m.M24) / w,
            (m.M31 * v.X + m.M32 * v.Y + m.M33 * v.Z + m.M34) / w
        );
    }

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

    public static bool operator !=(Matrix4x4 a, Matrix4x4 b) => !(a == b);

    public bool Equals(Matrix4x4 other) => this == other;

    public override bool Equals(object? obj) => obj is Matrix4x4 m && Equals(m);

    public override int GetHashCode() =>
        HashCode.Combine(
            HashCode.Combine(M11, M12, M13, M14, M21, M22, M23, M24),
            HashCode.Combine(M31, M32, M33, M34, M41, M42, M43, M44)
        );
}
