namespace SilkEngine.SourceGen.Diagnostics
{
    /// <summary>
    /// Diagnostic-id bands and the allocated ids of every generator in this analyzer assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This analyzer assembly is attached to more than one runtime project (B6), and several
    /// generators may run against the same compilation. Two generators must never claim the same
    /// id, otherwise the error list cannot tell which generator produced a report. Ids are therefore
    /// allocated per owner, in fixed bands:
    /// </para>
    /// <list type="bullet">
    ///   <item><description><c>SRC0xx</c> — rendering command generator (<c>RenderCommandSourceGen</c>): <c>SRC001</c>–<c>SRC099</c>.</description></item>
    ///   <item><description><c>SRC1xx</c> — mathematics value-type generator (proposal-04 B5): <c>SRC101</c>–<c>SRC199</c>.</description></item>
    /// </list>
    /// <para>
    /// An existing id never changes value or band: review reports and batch acceptance criteria
    /// quote the ids (for example <c>SRC002</c> for a duplicate command id). New ids are appended
    /// inside the owner's band and declared here as constants, because
    /// <see cref="Microsoft.CodeAnalysis.DiagnosticDescriptor"/> requires a non-null constant id
    /// (RS1017).
    /// </para>
    /// </remarks>
    internal static class DiagnosticBands
    {
        /// <summary>First id number of the rendering band (inclusive).</summary>
        internal const int RenderingStart = 1;

        /// <summary>Last id number of the rendering band (inclusive).</summary>
        internal const int RenderingEnd = 99;

        /// <summary>First id number of the mathematics band (inclusive).</summary>
        internal const int MathematicsStart = 101;

        /// <summary>Last id number of the mathematics band (inclusive).</summary>
        internal const int MathematicsEnd = 199;

        // ── SRC0xx: rendering command generator. SRC005 is intentionally unused (gap). ──

        /// <summary>Command id outside 0–255.</summary>
        internal const string RenderingIdOutOfRange = "SRC001";

        /// <summary>Two commands declare the same id.</summary>
        internal const string RenderingIdDuplicate = "SRC002";

        /// <summary>Executor does not have the required signature.</summary>
        internal const string RenderingInvalidExecutorSignature = "SRC003";

        /// <summary>Executors seen in one compilation use different <c>CommandContext</c> types.</summary>
        internal const string RenderingInconsistentCommandContext = "SRC004";

        /// <summary>Command struct is not unmanaged.</summary>
        internal const string RenderingNotUnmanaged = "SRC006";

        // ── SRC1xx: mathematics value-type generator. ──

        /// <summary>A <c>[MathValue]</c> component name does not resolve to a field of the struct.</summary>
        internal const string MathematicsInvalidComponent = "SRC101";

        /// <summary>A <c>[MathValue]</c> type mixes component types (the indexer assumes one type).</summary>
        internal const string MathematicsMixedComponentTypes = "SRC102";
    }
}
