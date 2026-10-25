using SilkEngine.Mathematics.Internal;

namespace SilkEngine;

/// <summary>
/// Euler-angle rotation in degrees with Unreal semantics: <see cref="Pitch"/> about +Y,
/// <see cref="Yaw"/> about +Z, <see cref="Roll"/> about +X.
/// </summary>
/// <remarks>
/// <para>
/// Sign convention (frozen contract, OBS-01): positive pitch = nose up (Forward +X → +Z), positive
/// yaw = turn right (Forward +X → +Y), positive roll = roll right (Right +Y → −Z).
/// </para>
/// <para>
/// This is the only Euler-angle vocabulary of the engine: the conversion chain is
/// <c>Rotator → Quaternion → Float4x4</c>. <see cref="Quaternion"/> itself stays in the standard
/// right-handed form, so <see cref="ToQuaternion"/> negates pitch and roll about their axes.
/// </para>
/// </remarks>
public readonly struct Rotator : IEquatable<Rotator>, IFormattable
{
    /// <summary>Rotation about +Y in degrees; positive = nose up.</summary>
    public readonly float Pitch;

    /// <summary>Rotation about +Z in degrees; positive = turn right.</summary>
    public readonly float Yaw;

    /// <summary>Rotation about +X in degrees; positive = roll right.</summary>
    public readonly float Roll;

    /// <summary>(0, 0, 0) — no rotation.</summary>
    public static readonly Rotator Zero = new(0f, 0f, 0f);

    /// <summary>Creates a rotator from degree angles; see the type remarks for the sign convention.</summary>
    /// <param name="pitch">Rotation about +Y in degrees.</param>
    /// <param name="yaw">Rotation about +Z in degrees.</param>
    /// <param name="roll">Rotation about +X in degrees.</param>
    public Rotator(float pitch, float yaw, float roll)
    {
        Pitch = pitch;
        Yaw = yaw;
        Roll = roll;
    }

    /// <summary>Creates a rotator from degree angles (named factory form).</summary>
    /// <param name="pitch">Rotation about +Y in degrees.</param>
    /// <param name="yaw">Rotation about +Z in degrees.</param>
    /// <param name="roll">Rotation about +X in degrees.</param>
    /// <returns>The rotator.</returns>
    public static Rotator FromDegrees(float pitch, float yaw, float roll) =>
        new(pitch, yaw, roll);

    /// <summary>
    /// Converts to a quaternion: <c>AngleAxis(Yaw,+Z) * AngleAxis(−Pitch,+Y) * AngleAxis(−Roll,+X)</c>.
    /// </summary>
    /// <returns>The equivalent rotation.</returns>
    public Quaternion ToQuaternion() =>
        Quaternion.AngleAxis(Yaw, Vector3.Up)
        * Quaternion.AngleAxis(-Pitch, Vector3.Right)
        * Quaternion.AngleAxis(-Roll, Vector3.Forward);

    /// <summary>Converts to a matrix; equals <c>ToQuaternion().ToMatrix()</c>.</summary>
    /// <returns>The equivalent row-vector matrix.</returns>
    public Float4x4 ToMatrix() => ToQuaternion().ToMatrix();

    /// <summary>Extracts the rotator from a quaternion (inverse of <see cref="ToQuaternion"/>).</summary>
    /// <param name="q">Source rotation.</param>
    /// <returns>The equivalent pitch/yaw/roll in degrees.</returns>
    /// <remarks>
    /// At gimbal lock (pitch = ±90°) the roll degree of freedom is folded into yaw (roll = 0), which
    /// keeps the mapping total. Round-trips within ±90° pitch are exact up to <c>1e-4</c> degrees.
    /// </remarks>
    public static Rotator FromQuaternion(in Quaternion q)
    {
        Float4x4 m = q.ToMatrix();

        float sp = EngineMath.Clamp(m.M02, -1f, 1f);
        float pitch = EngineMath.Asin(sp);
        float cp = EngineMath.Cos(pitch);

        float yaw,
            roll;
        if (cp > 1e-6f)
        {
            roll = EngineMath.Atan2(-m.M12, m.M22);
            yaw = EngineMath.Atan2(m.M01, m.M00);
        }
        else
        {
            roll = 0f;
            yaw = EngineMath.Atan2(-m.M10, m.M11);
        }

        return new Rotator(
            pitch * EngineMath.Rad2Deg,
            yaw * EngineMath.Rad2Deg,
            roll * EngineMath.Rad2Deg
        );
    }

    /// <summary>Exact component-wise equality (same contract as <see cref="Equals(Rotator)"/>).</summary>
    public static bool operator ==(Rotator lhs, Rotator rhs) =>
        lhs.Pitch == rhs.Pitch && lhs.Yaw == rhs.Yaw && lhs.Roll == rhs.Roll;

    public static bool operator !=(Rotator lhs, Rotator rhs) => !(lhs == rhs);

    public bool Equals(Rotator other) => this == other;

    public override bool Equals(object? obj) => obj is Rotator r && this == r;

    public override int GetHashCode() => HashUtil.Of(Pitch, Yaw, Roll);

    public override string ToString() => ToString(null, null);

    public string ToString(string format) => ToString(format, null);

    /// <summary>Formats the rotator as <c>Rotator(pitch, yaw, roll)</c>.</summary>
    /// <param name="format">Numeric format; <c>null</c>/empty falls back to <c>F3</c>.</param>
    /// <param name="formatProvider">Culture to format with; invariant culture when <c>null</c>.</param>
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        FormatUtil.Format("Rotator", "F3", format, formatProvider, Pitch, Yaw, Roll);
}
