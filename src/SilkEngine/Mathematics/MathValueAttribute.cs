using System;

namespace SilkEngine.Mathematics;

/// <summary>
/// Marks a math value type whose mechanical skeleton is generated: component indexer, exact equality,
/// hashing and <see cref="IFormattable"/> formatting.
/// </summary>
/// <remarks>
/// <para>
/// Only mechanical members are generated; mathematical operations, constants and conversions stay
/// handwritten. The generator resolves the component names, which therefore must be fields of the
/// annotated struct, and it emits one partial file per type.
/// </para>
/// <para>
/// The generated code keeps the engine-wide contracts: exact <c>==</c>/<c>Equals</c>/<c>GetHashCode</c>
/// and <c>FormatUtil</c> formatting with the given prefix and default format.
/// </para>
/// <para>
/// Layering: this attribute lives in <c>SilkEngine.Mathematics</c> (moving it here removed the last
/// root-namespace metadata dependency of the <c>Mathematics</c> assembly-internal layer); types in
/// the root namespace apply it through <c>using SilkEngine.Mathematics;</c>.
/// </para>
/// <para>
/// Constraint (MATH-Q-2): every listed component must have the <b>same field type</b> — the generated
/// indexer returns one element type. A mixed-type struct is rejected with diagnostic <c>SRC102</c>
/// instead of a lossy indexer.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Struct, Inherited = false)]
internal sealed class MathValueAttribute : Attribute
{
    /// <summary>Marks a type with a one-dimensional component indexer.</summary>
    /// <param name="prefix">Format prefix, usually the type name (<c>RGBA</c> for colors).</param>
    /// <param name="defaultFormat">Numeric format used when the caller passes none.</param>
    /// <param name="components">Field names in indexer order.</param>
    public MathValueAttribute(string prefix, string defaultFormat, params string[] components)
        : this(prefix, defaultFormat, 0, components) { }

    /// <summary>Marks a type with a two-dimensional <c>[row, col]</c> indexer over a row-major field list.</summary>
    /// <param name="prefix">Format prefix, usually the type name.</param>
    /// <param name="defaultFormat">Numeric format used when the caller passes none.</param>
    /// <param name="columns">Columns per row (4 for 4×4 matrices); the field list is row-major.</param>
    /// <param name="components">Field names in row-major order.</param>
    public MathValueAttribute(
        string prefix,
        string defaultFormat,
        int columns,
        params string[] components
    )
    {
        Prefix = prefix;
        DefaultFormat = defaultFormat;
        Columns = columns;
        Components = components;
    }

    /// <summary>Format prefix used by <c>ToString</c>.</summary>
    public string Prefix { get; }

    /// <summary>Numeric format used when the caller passes none.</summary>
    public string DefaultFormat { get; }

    /// <summary>Columns per row; <c>0</c> selects the one-dimensional indexer.</summary>
    public int Columns { get; }

    /// <summary>Field names in indexer (and formatting) order.</summary>
    public string[] Components { get; }
}
