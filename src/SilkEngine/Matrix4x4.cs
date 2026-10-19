namespace SilkEngine;

/// <summary>
/// 4x4 row-vector matrix (translations live in row 3).
/// Axis convention matches Unreal: +X Forward, +Y Right, +Z Up.
/// </summary>
public struct Matrix4x4
{
    public float M00,
        M01,
        M02,
        M03;
    public float M10,
        M11,
        M12,
        M13;
    public float M20,
        M21,
        M22,
        M23;
    public float M30,
        M31,
        M32,
        M33;

    /// <summary>
    /// Returns a identity matrix4x4.
    /// <br/>(1f, 0, 0, 0)
    /// <br/>(0, 1f, 0, 0)
    /// <br/>(0, 0, 1f, 0)
    /// <br/>(0, 0, 0, 1f)
    /// </summary>
    public static readonly Matrix4x4 Identity = new(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);

    public Matrix4x4(
        float m00,
        float m01,
        float m02,
        float m03,
        float m10,
        float m11,
        float m12,
        float m13,
        float m20,
        float m21,
        float m22,
        float m23,
        float m30,
        float m31,
        float m32,
        float m33
    )
    {
        M00 = m00;
        M01 = m01;
        M02 = m02;
        M03 = m03;
        M10 = m10;
        M11 = m11;
        M12 = m12;
        M13 = m13;
        M20 = m20;
        M21 = m21;
        M22 = m22;
        M23 = m23;
        M30 = m30;
        M31 = m31;
        M32 = m32;
        M33 = m33;
    }

    public float this[int row, int col]
    {
        get =>
            (row, col) switch
            {
                (0, 0) => M00,
                (0, 1) => M01,
                (0, 2) => M02,
                (0, 3) => M03,
                (1, 0) => M10,
                (1, 1) => M11,
                (1, 2) => M12,
                (1, 3) => M13,
                (2, 0) => M20,
                (2, 1) => M21,
                (2, 2) => M22,
                (2, 3) => M23,
                (3, 0) => M30,
                (3, 1) => M31,
                (3, 2) => M32,
                (3, 3) => M33,
                _ => throw new IndexOutOfRangeException(),
            };
        set
        {
            switch ((row, col))
            {
                case (0, 0):
                    M00 = value;
                    break;
                case (0, 1):
                    M01 = value;
                    break;
                case (0, 2):
                    M02 = value;
                    break;
                case (0, 3):
                    M03 = value;
                    break;
                case (1, 0):
                    M10 = value;
                    break;
                case (1, 1):
                    M11 = value;
                    break;
                case (1, 2):
                    M12 = value;
                    break;
                case (1, 3):
                    M13 = value;
                    break;
                case (2, 0):
                    M20 = value;
                    break;
                case (2, 1):
                    M21 = value;
                    break;
                case (2, 2):
                    M22 = value;
                    break;
                case (2, 3):
                    M23 = value;
                    break;
                case (3, 0):
                    M30 = value;
                    break;
                case (3, 1):
                    M31 = value;
                    break;
                case (3, 2):
                    M32 = value;
                    break;
                case (3, 3):
                    M33 = value;
                    break;
                default:
                    throw new IndexOutOfRangeException();
            }
        }
    }

    /// <summary>Creates a translation matrix.</summary>
    public static Matrix4x4 Translate(float x, float y, float z)
    {
        var m = Identity;
        m.M30 = x;
        m.M31 = y;
        m.M32 = z;
        return m;
    }

    public static Matrix4x4 Translate(Vector3 v) => Translate(v.X, v.Y, v.Z);

    /// <summary>Creates a non-uniform scale matrix.</summary>
    public static Matrix4x4 Scale(float x, float y, float z) =>
        new(x, 0, 0, 0, 0, y, 0, 0, 0, 0, z, 0, 0, 0, 0, 1);

    /// <summary>Creates a uniform scale matrix.</summary>
    public static Matrix4x4 Scale(float s) => Scale(s, s, s);

    /// <summary>Rotation matrix around the Z axis (yaw).</summary>
    public static Matrix4x4 Yaw(float degrees)
    {
        float r = degrees * EngineMath.PI / 180f;
        float c = EngineMath.Cos(r),
            s = EngineMath.Sin(r);
        return new Matrix4x4(c, s, 0, 0, -s, c, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);
    }

    /// <summary>Rotation matrix around the Y axis (pitch).</summary>
    public static Matrix4x4 Pitch(float degrees)
    {
        float r = degrees * EngineMath.PI / 180f;
        float c = EngineMath.Cos(r),
            s = EngineMath.Sin(r);
        return new Matrix4x4(c, 0, s, 0, 0, 1, 0, 0, -s, 0, c, 0, 0, 0, 0, 1);
    }

    /// <summary>Rotation matrix around the X axis (roll).</summary>
    public static Matrix4x4 Roll(float degrees)
    {
        float r = degrees * EngineMath.PI / 180f;
        float c = EngineMath.Cos(r),
            s = EngineMath.Sin(r);
        return new Matrix4x4(1, 0, 0, 0, 0, c, -s, 0, 0, s, c, 0, 0, 0, 0, 1);
    }

    /// <summary>
    /// Composes roll * pitch * yaw using the engine's Unreal-style axes.
    /// </summary>
    public static Matrix4x4 FromRotator(float pitchDeg, float yawDeg, float rollDeg) =>
        Roll(rollDeg) * Pitch(pitchDeg) * Yaw(yawDeg);

    /// <summary>
    /// Builds a row-vector rotation matrix from a quaternion. Inverse of <see cref="Quaternion.FromMatrix"/>.
    /// Satisfies: v * Rotate(q) == q * v.
    /// </summary>
    public static Matrix4x4 Rotate(Quaternion q)
    {
        float x = q.X,
            y = q.Y,
            z = q.Z,
            w = q.W;
        float xx = x * 2f * x,
            yy = y * 2f * y,
            zz = z * 2f * z;
        float xy = x * 2f * y,
            xz = x * 2f * z,
            yz = y * 2f * z;
        float wx = w * 2f * x,
            wy = w * 2f * y,
            wz = w * 2f * z;

        return new Matrix4x4(
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

    /// <summary>Convenience: scale * rotation * translation.</summary>
    public static Matrix4x4 TRS(Vector3 location, Vector3 rotPitchYawRollDeg, Vector3 scale) =>
        Scale(scale.X, scale.Y, scale.Z)
        * FromRotator(rotPitchYawRollDeg.X, rotPitchYawRollDeg.Y, rotPitchYawRollDeg.Z)
        * Translate(location);

    public void SetTRS(Vector3 location, Vector3 rotPitchYawRollDeg, Vector3 scale) =>
        this = TRS(location, rotPitchYawRollDeg, scale);

    public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b) =>
        new(
            a.M00 * b.M00 + a.M01 * b.M10 + a.M02 * b.M20 + a.M03 * b.M30,
            a.M00 * b.M01 + a.M01 * b.M11 + a.M02 * b.M21 + a.M03 * b.M31,
            a.M00 * b.M02 + a.M01 * b.M12 + a.M02 * b.M22 + a.M03 * b.M32,
            a.M00 * b.M03 + a.M01 * b.M13 + a.M02 * b.M23 + a.M03 * b.M33,
            a.M10 * b.M00 + a.M11 * b.M10 + a.M12 * b.M20 + a.M13 * b.M30,
            a.M10 * b.M01 + a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31,
            a.M10 * b.M02 + a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32,
            a.M10 * b.M03 + a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33,
            a.M20 * b.M00 + a.M21 * b.M10 + a.M22 * b.M20 + a.M23 * b.M30,
            a.M20 * b.M01 + a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31,
            a.M20 * b.M02 + a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32,
            a.M20 * b.M03 + a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33,
            a.M30 * b.M00 + a.M31 * b.M10 + a.M32 * b.M20 + a.M33 * b.M30,
            a.M30 * b.M01 + a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31,
            a.M30 * b.M02 + a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32,
            a.M30 * b.M03 + a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33
        );

    /// <summary>Returns the transpose of this matrix.</summary>
    public Matrix4x4 Transposed() =>
        new(M00, M10, M20, M30, M01, M11, M21, M31, M02, M12, M22, M32, M03, M13, M23, M33);

    /// <summary>Unity 命名：返回转置（语义同 <see cref="Transposed"/>）。</summary>
    public Matrix4x4 transpose => Transposed();

    /// <summary>Returns the inverse of this matrix. Throws if singular.</summary>
    public Matrix4x4 Inverse
    {
        get
        {
            float a00 = M00,
                a01 = M01,
                a02 = M02,
                a03 = M03;
            float a10 = M10,
                a11 = M11,
                a12 = M12,
                a13 = M13;
            float a20 = M20,
                a21 = M21,
                a22 = M22,
                a23 = M23;
            float a30 = M30,
                a31 = M31,
                a32 = M32,
                a33 = M33;

            float b00 = a00 * a11 - a01 * a10;
            float b01 = a00 * a12 - a02 * a10;
            float b02 = a00 * a13 - a03 * a10;
            float b03 = a01 * a12 - a02 * a11;
            float b04 = a01 * a13 - a03 * a11;
            float b05 = a02 * a13 - a03 * a12;
            float b06 = a20 * a31 - a21 * a30;
            float b07 = a20 * a32 - a22 * a30;
            float b08 = a20 * a33 - a23 * a30;
            float b09 = a21 * a32 - a22 * a31;
            float b10 = a21 * a33 - a23 * a31;
            float b11 = a22 * a33 - a23 * a32;

            float det = b00 * b11 - b01 * b10 + b02 * b09 + b03 * b08 - b04 * b07 + b05 * b06;
            if (EngineMath.Abs(det) < 1e-10f)
                throw new InvalidOperationException("Matrix is not invertible (det ≈ 0).");
            float id = 1f / det;

            return new Matrix4x4(
                (a11 * b11 - a12 * b10 + a13 * b09) * id,
                (a02 * b10 - a01 * b11 - a03 * b09) * id,
                (a31 * b05 - a32 * b04 + a33 * b03) * id,
                (a22 * b04 - a21 * b05 - a23 * b03) * id,
                (a12 * b08 - a10 * b11 - a13 * b07) * id,
                (a00 * b11 - a02 * b08 + a03 * b07) * id,
                (a32 * b02 - a30 * b05 - a33 * b01) * id,
                (a20 * b05 - a22 * b02 + a23 * b01) * id,
                (a10 * b10 - a11 * b08 + a13 * b06) * id,
                (a01 * b08 - a00 * b10 - a03 * b06) * id,
                (a30 * b04 - a31 * b02 + a33 * b00) * id,
                (a21 * b02 - a20 * b04 - a23 * b00) * id,
                (a11 * b07 - a10 * b09 - a12 * b06) * id,
                (a00 * b09 - a01 * b07 + a02 * b06) * id,
                (a31 * b01 - a30 * b03 - a32 * b00) * id,
                (a20 * b03 - a21 * b01 + a22 * b00) * id
            );
        }
    }

    /// <summary>Transforms a point (applies rotation and translation).</summary>
    public Vector3 TransformPoint(Vector3 p) =>
        new(
            p.X * M00 + p.Y * M10 + p.Z * M20 + M30,
            p.X * M01 + p.Y * M11 + p.Z * M21 + M31,
            p.X * M02 + p.Y * M12 + p.Z * M22 + M32
        );

    public Vector3 MultiplyPoint3x4(Vector3 p) => TransformPoint(p);

    public Vector3 MultiplyPoint(Vector3 p) => TransformPoint(p);

    /// <summary>Transforms a direction (ignores translation).</summary>
    public Vector3 TransformVector(Vector3 v) =>
        new(
            v.X * M00 + v.Y * M10 + v.Z * M20,
            v.X * M01 + v.Y * M11 + v.Z * M21,
            v.X * M02 + v.Y * M12 + v.Z * M22
        );

    public Vector3 MultiplyVector(Vector3 v) => TransformVector(v);

    public Vector4 TransformVector4(Vector4 v) =>
        new(
            v.X * M00 + v.Y * M10 + v.Z * M20 + v.W * M30,
            v.X * M01 + v.Y * M11 + v.Z * M21 + v.W * M31,
            v.X * M02 + v.Y * M12 + v.Z * M22 + v.W * M32,
            v.X * M03 + v.Y * M13 + v.Z * M23 + v.W * M33
        );

    public override string ToString() =>
        $"[{M00, 7:F3} {M01, 7:F3} {M02, 7:F3} {M03, 7:F3}]\n"
        + $"[{M10, 7:F3} {M11, 7:F3} {M12, 7:F3} {M13, 7:F3}]\n"
        + $"[{M20, 7:F3} {M21, 7:F3} {M22, 7:F3} {M23, 7:F3}]\n"
        + $"[{M30, 7:F3} {M31, 7:F3} {M32, 7:F3} {M33, 7:F3}]";
}
