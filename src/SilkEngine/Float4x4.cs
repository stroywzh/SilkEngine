using SilkEngine.Mathematics;

namespace SilkEngine;

/// <summary>
/// Row-vector 4x4 matrix. Translation lives in row 3 (M30..M32). Compose as <c>v * M</c>.
/// Not layout-compatible with Unity or System.Numerics four-by-four matrices: those are
/// column-vector types with the translation in the last column, this type is row-vector with the
/// translation in row 3 (M30..M32).
/// </summary>
/// <remarks>
/// Pure value type: <c>readonly struct</c> with <c>public readonly</c> fields, no in-place mutators.
/// <c>operator ==</c>, <see cref="Equals(Float4x4)"/> and <see cref="GetHashCode"/> are exact and
/// mutually consistent; component-wise tolerance comparison is intentionally not provided because a
/// matrix tolerance has to be defined by the caller's use case.
/// <para>
/// The only rotation conversions are <see cref="Quaternion.ToMatrix"/> and
/// <see cref="Quaternion.FromMatrix"/>; there is no Euler-angle factory or accessor (rotation
/// vocabulary is <see cref="Rotator"/>).
/// </para>
/// </remarks>
[MathValue(
    "Float4x4",
    "F3",
    4,
    "M00",
    "M01",
    "M02",
    "M03",
    "M10",
    "M11",
    "M12",
    "M13",
    "M20",
    "M21",
    "M22",
    "M23",
    "M30",
    "M31",
    "M32",
    "M33"
)]
public readonly partial struct Float4x4 : IEquatable<Float4x4>, IFormattable
{
    public readonly float M00,
        M01,
        M02,
        M03;
    public readonly float M10,
        M11,
        M12,
        M13;
    public readonly float M20,
        M21,
        M22,
        M23;
    public readonly float M30,
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
    public static readonly Float4x4 Identity = new(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);

    public Float4x4(
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

    /// <summary>Creates a translation matrix (translation in row 3).</summary>
    /// <param name="translation">Translation vector.</param>
    public static Float4x4 FromTranslation(in Vector3 translation) =>
        new(
            1,
            0,
            0,
            0,
            0,
            1,
            0,
            0,
            0,
            0,
            1,
            0,
            translation.X,
            translation.Y,
            translation.Z,
            1
        );

    /// <summary>Creates a non-uniform scale matrix.</summary>
    /// <param name="scale">Per-axis scale factors.</param>
    public static Float4x4 FromScale(in Vector3 scale) =>
        new(scale.X, 0, 0, 0, 0, scale.Y, 0, 0, 0, 0, scale.Z, 0, 0, 0, 0, 1);

    /// <summary>Creates a uniform scale matrix.</summary>
    /// <param name="scale">Scale factor applied to every axis.</param>
    public static Float4x4 FromUniformScale(float scale) =>
        FromScale(new Vector3(scale, scale, scale));

    /// <summary>Composes scale * rotation * translation.</summary>
    /// <param name="translation">World/local translation.</param>
    /// <param name="rotation">Rotation as a quaternion (the only rotation input).</param>
    /// <param name="scale">Per-axis scale.</param>
    /// <returns>The composed row-vector matrix.</returns>
    public static Float4x4 FromTRS(
        in Vector3 translation,
        in Quaternion rotation,
        in Vector3 scale
    )
    {
        return FromScale(scale) * rotation.ToMatrix() * FromTranslation(translation);
    }

    public static Float4x4 operator *(Float4x4 a, Float4x4 b) =>
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

    /// <summary>Returns this matrix with rows and columns swapped.</summary>
    public Float4x4 Transposed() =>
        new(M00, M10, M20, M30, M01, M11, M21, M31, M02, M12, M22, M32, M03, M13, M23, M33);

    /// <summary>Returns the inverse of this matrix.</summary>
    /// <returns>The inverted matrix.</returns>
    /// <exception cref="InvalidOperationException">The matrix is singular (determinant ≈ 0).</exception>
    /// <remarks>Inversion is a full recomputation, which is why it is a method and not a property.</remarks>
    public Float4x4 Inverted()
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

        return new Float4x4(
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

    /// <summary>Transforms a point (applies rotation and translation).</summary>
    public Vector3 TransformPoint(Vector3 p) =>
        new(
            p.X * M00 + p.Y * M10 + p.Z * M20 + M30,
            p.X * M01 + p.Y * M11 + p.Z * M21 + M31,
            p.X * M02 + p.Y * M12 + p.Z * M22 + M32
        );

    /// <summary>Transforms a direction (ignores translation).</summary>
    public Vector3 TransformVector(Vector3 v) =>
        new(
            v.X * M00 + v.Y * M10 + v.Z * M20,
            v.X * M01 + v.Y * M11 + v.Z * M21,
            v.X * M02 + v.Y * M12 + v.Z * M22
        );

    /// <summary>Transforms a homogeneous vector (uses the full matrix, including the W row/column).</summary>
    public Vector4 TransformVector4(Vector4 v) =>
        new(
            v.X * M00 + v.Y * M10 + v.Z * M20 + v.W * M30,
            v.X * M01 + v.Y * M11 + v.Z * M21 + v.W * M31,
            v.X * M02 + v.Y * M12 + v.Z * M22 + v.W * M32,
            v.X * M03 + v.Y * M13 + v.Z * M23 + v.W * M33
        );

}
