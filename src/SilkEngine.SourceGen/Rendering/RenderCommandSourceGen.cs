using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using SilkEngine.SourceGen.Diagnostics;

namespace SilkEngine.SourceGen.Rendering
{
    /// <summary>
    /// Generates the per-assembly command table, the assembly-local recorder and the typed recording
    /// extensions for every <c>[RenderCommand]</c> declaration (C-07, REN-N-01).
    /// </summary>
    /// <remarks>
    /// Inputs are kept as two incremental outputs (REN-N-02): the table depends only on the collected
    /// commands plus the "hosts a backend" gate, the extensions additionally on the compilation owning
    /// <c>ICommandBuffer</c>.
    /// </remarks>
    [Generator(LanguageNames.CSharp)]
    public class RenderCommandSourceGen : IIncrementalGenerator
    {
        private const string AttrMetadataName =
            "SilkEngine.Rendering.Attributes.RenderCommandAttribute";

        private const string CommandBufferMetadataName =
            "SilkEngine.Rendering.Abstraction.ICommandBuffer";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var commands = context
                .SyntaxProvider.ForAttributeWithMetadataName(
                    AttrMetadataName,
                    static (node, _) => node is StructDeclarationSyntax,
                    static (ctx, ct) => Extract(ctx, ct)
                )
                .Where(static m => m is not null)
                .Select(static (m, _) => m!)
                .Collect();

            // REN-N-02: the table depends only on the collected commands plus a cheap boolean gate.
            // REN-N-01 adds that gate: a backend assembly needs a (possibly empty) table even before
            // its first command exists.
            var needsTable = context.CompilationProvider.Select(
                static (compilation, _) => NeedsEmptyTable(compilation)
            );
            var tableInput = commands.Combine(needsTable);
            context.RegisterSourceOutput(
                tableInput,
                static (spc, pair) => EmitTable(spc, pair.Left, pair.Right)
            );

            // C-07 + REN-N-02: the recording extensions additionally depend on whether this assembly
            // owns ICommandBuffer, but that gate stays a cheap boolean select.
            var ownsCommandBuffer = context.CompilationProvider.Select(
                static (compilation, _) => OwnsCommandBuffer(compilation)
            );
            var extensions = commands.Combine(ownsCommandBuffer);
            context.RegisterSourceOutput(
                extensions,
                static (spc, pair) => EmitExtensions(spc, pair.Left, pair.Right)
            );
        }

        // ───────────────────────── 提取 & 校验 ─────────────────────────
        private static CommandModel? Extract(
            GeneratorAttributeSyntaxContext ctx,
            CancellationToken ct
        )
        {
            if (ctx.TargetSymbol is not INamedTypeSymbol structSym)
                return null;
            if (structSym.TypeKind != TypeKind.Struct)
                return null;

            var attr = ctx.Attributes.FirstOrDefault();
            if (attr is null || attr.ConstructorArguments.Length < 2)
                return null;
            if (attr.ConstructorArguments[0].Value is not int id)
                return null;
            if (attr.ConstructorArguments[1].Value is not INamedTypeSymbol executorSym)
                return null;

            var loc = structSym.Locations.FirstOrDefault() ?? Location.None;
            var diags = ImmutableArray.CreateBuilder<Diagnostic>();

            if (id < 0 || id > 255)
                diags.Add(Diagnostic.Create(Diags.IdOutOfRange, loc, structSym.Name, id));

            var execFullName = executorSym.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat
            );

            string? ctxFullName = null;
            var execLoc = executorSym.Locations.FirstOrDefault(l => l.IsInSource) ?? loc;
            if (!TryGetExecutorSignature(executorSym, out ctxFullName))
                diags.Add(
                    Diagnostic.Create(
                        Diags.ExecutorSignature,
                        execLoc,
                        executorSym.Name,
                        structSym.Name
                    )
                );

            if (!structSym.IsUnmanagedType)
                diags.Add(Diagnostic.Create(Diags.NotUnmanaged, loc, structSym.Name));

            var diagArr = diags.ToImmutable();
            var diagSig = string.Join(
                ";",
                diagArr.Select(d => d.Id + "|" + d.GetMessage(CultureInfo.InvariantCulture))
            );

            return new CommandModel
            {
                TypeName = structSym.Name,
                CommandFullName = structSym.ToDisplayString(
                    SymbolDisplayFormat.FullyQualifiedFormat
                ),
                CommandNamespace = structSym.ContainingNamespace?.ToDisplayString() ?? "",
                IsPublic = structSym.DeclaredAccessibility == Accessibility.Public,
                ExecutorFullName = execFullName,
                CommandContextFullName = ctxFullName,
                Id = id,
                Loc = loc,
                Position = new SourcePosition(
                    loc.SourceTree?.FilePath ?? "",
                    loc.SourceSpan.Start,
                    loc.SourceSpan.Length
                ),
                Diagnostics = diagArr,
                DiagnosticsSignature = diagSig,
            };
        }

        private static bool TryGetExecutorSignature(INamedTypeSymbol exec, out string? ctxFullName)
        {
            ctxFullName = null;

            if (exec.TypeKind != TypeKind.Class || !exec.IsStatic)
                return false;
            if (exec.IsGenericType)
                return false;

            var m = exec.GetMembers("Execute").OfType<IMethodSymbol>().FirstOrDefault();
            if (m is null || !m.IsStatic || m.IsGenericMethod)
                return false;
            if (m.ReturnType.SpecialType != SpecialType.System_Void)
                return false;
            if (m.Parameters.Length != 2)
                return false;
            if (m.DeclaredAccessibility < Accessibility.Internal)
                return false;

            // p0: ReadOnlySpan<byte>
            var p0 = m.Parameters[0];
            if (p0.RefKind != RefKind.None)
                return false;
            if (
                p0.Type is not INamedTypeSymbol n0
                || n0.Name != "ReadOnlySpan"
                || n0.Arity != 1
                || n0.ContainingNamespace.ToDisplayString() != "System"
                || n0.TypeArguments[0].SpecialType != SpecialType.System_Byte
            )
                return false;

            // p1: ref CommandContext（命名空间不限，类型完整名由签名提取）
            var p1 = m.Parameters[1];
            if (p1.RefKind != RefKind.Ref)
                return false;
            if (
                p1.Type is not INamedTypeSymbol n1
                || n1.Name != "CommandContext"
                || n1.TypeKind != TypeKind.Struct
            )
                return false;

            ctxFullName = n1.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            return true;
        }

        // ───────────────────────── 生成 ─────────────────────────
        private static void EmitTable(
            SourceProductionContext spc,
            ImmutableArray<CommandModel> models,
            bool needsEmptyTable
        )
        {
            // PR-A convention: a generator that finds no input emits no files and reports no
            // diagnostics. This analyzer is attached to every runtime project (B6), so it has to be
            // a no-op in compilations without [RenderCommand] declarations — except in an assembly
            // that hosts a render backend: it needs an empty table so its override of Dispatch binds
            // to RenderCommandTable before the first command exists (REN-N-01).
            if (models.IsDefaultOrEmpty)
            {
                if (needsEmptyTable)
                {
                    spc.AddSource(
                        "RenderCommandTable.g.cs",
                        EmitCommandTable(
                            new List<CommandModel>(),
                            "global::SilkEngine.Rendering.CommandContext",
                            "SilkEngine.Rendering"
                        )
                    );
                }

                return;
            }

            foreach (var m in models)
            foreach (var d in m.Diagnostics)
                spc.ReportDiagnostic(d);

            var valid = ValidModels(models, spc);

            // ④ ctxFullName 一致性（仍产出表，避免后端因缺表再报 CS0103）
            var ctxNames = valid.Select(c => c.CommandContextFullName!).Distinct().ToList();
            if (ctxNames.Count > 1)
            {
                spc.ReportDiagnostic(
                    Diagnostic.Create(
                        Diags.InconsistentCommandContext,
                        Location.None,
                        string.Join(", ", ctxNames)
                    )
                );
            }

            var ctxFullName =
                ctxNames.FirstOrDefault() ?? "global::SilkEngine.Rendering.CommandContext";

            // ⑤ 命令表：即使没有有效命令也产出（空表 TryDispatch 返回 false，基类据此抛 RenderDeviceException）
            var tableNamespace = valid
                .Select(c => c.CommandNamespace)
                .FirstOrDefault(ns => !string.IsNullOrEmpty(ns));
            if (string.IsNullOrEmpty(tableNamespace))
                tableNamespace = models[0].CommandNamespace;
            if (string.IsNullOrEmpty(tableNamespace))
                tableNamespace = "SilkEngine.Rendering";

            spc.AddSource(
                "RenderCommandTable.g.cs",
                EmitCommandTable(valid, ctxFullName, tableNamespace!)
            );

            // REN-N-01: the declaring assembly also gets an assembly-local, non-extension recorder, so
            // a backend can record its own commands without hand-writing MemoryMarshal plumbing.
            spc.AddSource("RenderCommandRecorder.g.cs", EmitRecorder(valid, tableNamespace!));
        }

        /// <summary>
        /// Whether this compilation needs a command table even without any [RenderCommand] declaration:
        /// it hosts a render backend (REN-N-01). Detected from the syntax (a type deriving from
        /// RenderBackendBase), so the gate stays a cheap boolean input for the incremental pipeline.
        /// </summary>
        private static bool NeedsEmptyTable(Compilation compilation)
        {
            // The engine assembly always declares commands; skip the scan there.
            var buffer = compilation.GetTypeByMetadataName(CommandBufferMetadataName);
            if (
                buffer is not null
                && SymbolEqualityComparer.Default.Equals(buffer.ContainingAssembly, compilation.Assembly)
            )
                return false;

            foreach (var tree in compilation.SyntaxTrees)
            {
                foreach (var node in tree.GetRoot().DescendantNodes())
                {
                    if (
                        node is TypeDeclarationSyntax type
                        && type.BaseList is not null
                        && type.BaseList.Types.Any(t =>
                            t.Type.ToString() == "RenderBackendBase"
                            || t.Type.ToString() == "SilkEngine.Rendering.RenderBackendBase"
                        )
                    )
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Emits the assembly-local recorder: one static method per command, taking the buffer and the
        /// command by <c>in</c>. Deliberately not extension methods — a second extension class is the
        /// CS0433 trap the extensions split was made to avoid (C-07).
        /// </summary>
        private static SourceText EmitRecorder(List<CommandModel> cmds, string ns)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("namespace " + ns + ";");
            sb.AppendLine();
            sb.AppendLine("/// <summary>Assembly-local recording helpers for [RenderCommand] structs (REN-N-01).</summary>");
            sb.AppendLine("internal static class RenderCommandRecorder");
            sb.AppendLine("{");
            foreach (var c in cmds)
            {
                sb.Append(
                        "    public static void Record(global::SilkEngine.Rendering.Abstraction.ICommandBuffer buffer, in "
                    )
                    .Append(c.CommandFullName)
                    .AppendLine(" command)");
                sb.AppendLine("    {");
                sb.Append("        buffer.Record(").Append(c.Id).AppendLine(",");
                sb.AppendLine("            global::System.Runtime.InteropServices.MemoryMarshal.AsBytes(");
                sb.AppendLine("                global::System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(");
                sb.AppendLine("                    ref global::System.Runtime.CompilerServices.Unsafe.AsRef(in command), 1)));");
                sb.AppendLine("    }");
            }
            sb.AppendLine("}");
            return SourceText.From(sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Emits the strongly typed recording extensions, only in the assembly that owns
        /// <c>ICommandBuffer</c> (C-07), grouped by command namespace.
        /// </summary>
        private static void EmitExtensions(
            SourceProductionContext spc,
            ImmutableArray<CommandModel> models,
            bool ownsCommandBuffer
        )
        {
            if (!ownsCommandBuffer || models.IsDefaultOrEmpty)
                return;

            var valid = ValidModels(models, null);
            if (valid.Count == 0)
                return;

            var groups = valid
                .GroupBy(c => c.CommandNamespace ?? "SilkEngine.Rendering")
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .ToList();
            for (var i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                // REN-R-5: the hint name is derived from the namespace, not from the group index, so
                // adding/moving a command cannot rename (and thereby duplicate) a generated file.
                var hint =
                    groups.Count == 1
                        ? "RenderCommandExtensions.g.cs"
                        : "RenderCommandExtensions." + SanitizeNamespace(group.Key) + ".g.cs";
                spc.AddSource(hint, EmitExtensions(group.Key, group.ToList()));
            }
        }

        /// <summary>Stable file-name fragment of a namespace (dots and other characters become '_').</summary>
        private static string SanitizeNamespace(string ns)
        {
            var builder = new StringBuilder(ns.Length);
            foreach (var ch in ns)
            {
                builder.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            }

            return builder.ToString();
        }

        /// <summary>
        /// Filters the collected models: drops models with error diagnostics and duplicate ids, orders by
        /// id. Duplicate-id diagnostics are reported only when a sink is supplied (the table output owns
        /// them; the extensions output must not report them twice).
        /// </summary>
        private static List<CommandModel> ValidModels(
            ImmutableArray<CommandModel> models,
            SourceProductionContext? sink
        )
        {
            // ① 先过滤，只对无 Error 的模型查重
            var errorFree = models
                .Where(m => !m.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
                .ToList();

            // ② 在 errorFree 上查重复 ID
            var duplicateIds = new HashSet<int>();
            foreach (var g in errorFree.GroupBy(x => x.Id))
            {
                if (g.Count() <= 1)
                    continue;
                duplicateIds.Add(g.Key);
                var first = g.First().TypeName;
                foreach (var dup in g.Skip(1))
                    sink?.ReportDiagnostic(
                        Diagnostic.Create(
                            Diags.IdDuplicate,
                            dup.Loc ?? Location.None, // ← 不是 dup.Locations
                            dup.TypeName,
                            first,
                            dup.Id
                        )
                    );
            }

            // ③ valid：errorFree 减去 duplicateIds
            return errorFree
                .Where(m => m.CommandContextFullName is not null)
                .Where(m => !duplicateIds.Contains(m.Id))
                .OrderBy(m => m.Id)
                .ToList();
        }

        /// <summary>Whether this compilation defines the recording entry type (the engine assembly).</summary>
        private static bool OwnsCommandBuffer(Compilation compilation)
        {
            var buffer = compilation.GetTypeByMetadataName(CommandBufferMetadataName);
            return buffer is not null
                && SymbolEqualityComparer.Default.Equals(buffer.ContainingAssembly, compilation.Assembly);
        }

        private static SourceText EmitExtensions(string ns, List<CommandModel> cmds)
        {
            var isPublic = cmds.All(c => c.IsPublic);
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("namespace " + ns + ";");
            sb.AppendLine();
            sb.AppendLine("/// <summary>Strongly typed recording helpers generated from [RenderCommand] declarations.</summary>");
            sb.AppendLine((isPublic ? "public" : "internal") + " static class RenderCommandExtensions");
            sb.AppendLine("{");
            foreach (var c in cmds)
            {
                sb.Append(
                        "    public static void Record(this global::SilkEngine.Rendering.Abstraction.ICommandBuffer buffer, in "
                    )
                    .Append(c.CommandFullName)
                    .AppendLine(" command)");
                sb.AppendLine("    {");
                sb.Append("        buffer.Record(")
                    .Append(c.Id)
                    .AppendLine(
                        ", global::System.Runtime.InteropServices.MemoryMarshal.AsBytes("
                            + "global::System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan("
                            + "ref global::System.Runtime.CompilerServices.Unsafe.AsRef(in command), 1)));"
                    );
                sb.AppendLine("    }");
            }
            sb.AppendLine("}");
            return SourceText.From(sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Emits this assembly's command table. One table per assembly (C-07): two assemblies may each
        /// declare their own internal table with the same name, and each resolves to its own.
        /// </summary>
        private static SourceText EmitCommandTable(
            List<CommandModel> cmds,
            string ctxFullName,
            string ns
        )
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("namespace " + ns + ";");
            sb.AppendLine();
            sb.AppendLine("/// <summary>Per-assembly command dispatch table generated from [RenderCommand] declarations.</summary>");
            sb.AppendLine("internal static class RenderCommandTable");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>Dispatches one recorded command.</summary>");
            sb.AppendLine("    /// <param name=\"id\">Recorded command id.</param>");
            sb.AppendLine("    /// <param name=\"payload\">Raw bytes of the command struct.</param>");
            sb.AppendLine("    /// <param name=\"ctx\">Execution context.</param>");
            sb.AppendLine("    /// <returns><c>true</c> when an executor ran; <c>false</c> when this assembly has none for the id.</returns>");
            sb.Append(
                    "    public static bool TryDispatch(byte id, global::System.ReadOnlySpan<byte> payload, ref "
                )
                .Append(ctxFullName)
                .AppendLine(" ctx)");
            sb.AppendLine("    {");
            sb.AppendLine("        switch (id)");
            sb.AppendLine("        {");
            foreach (var c in cmds)
            {
                sb.Append("            case ").Append(c.Id).AppendLine(":");
                sb.Append("                ")
                    .Append(c.ExecutorFullName)
                    .AppendLine(".Execute(payload, ref ctx);");
                sb.AppendLine("                return true;");
            }
            sb.AppendLine("            default:");
            sb.AppendLine("                return false;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return SourceText.From(sb.ToString(), Encoding.UTF8);
        }

        // ───────────────────────── 模型 & 诊断 ─────────────────────────

        /// <summary>
        /// Comparable source position of a command declaration. Participates in the incremental
        /// equality so that moving a command inside its file re-runs the node and refreshes the
        /// diagnostic locations (REV-049).
        /// </summary>
        internal readonly record struct SourcePosition(string File, int Start, int Length);

        internal sealed record CommandModel
        {
            public string TypeName { get; init; } = "";
            public string CommandFullName { get; init; } = "";
            public string CommandNamespace { get; init; } = "";
            public bool IsPublic { get; init; }
            public string ExecutorFullName { get; init; } = "";
            public string? CommandContextFullName { get; init; }
            public int Id { get; init; }
            public Location? Loc { get; init; }
            public SourcePosition Position { get; init; }
            public ImmutableArray<Diagnostic> Diagnostics { get; init; }
            public string DiagnosticsSignature { get; init; } = "";

            public bool Equals(CommandModel? other) =>
                other is not null
                && CommandFullName == other.CommandFullName
                && CommandNamespace == other.CommandNamespace
                && IsPublic == other.IsPublic
                && ExecutorFullName == other.ExecutorFullName
                && CommandContextFullName == other.CommandContextFullName
                && Id == other.Id
                && Position == other.Position
                && DiagnosticsSignature == other.DiagnosticsSignature;

            public override int GetHashCode() =>
                HashCode.Combine(
                    CommandFullName,
                    CommandNamespace,
                    IsPublic,
                    ExecutorFullName,
                    CommandContextFullName,
                    Id,
                    Position,
                    DiagnosticsSignature
                );
        }

        /// <summary>Diagnostics of the rendering generator; ids live in the <c>SRC0xx</c> band.</summary>
        internal static class Diags
        {
            private const string Cat = "SilkEngine.Render";

            public static readonly DiagnosticDescriptor IdOutOfRange = new(
                DiagnosticBands.RenderingIdOutOfRange,
                "Command id must be 0–255",
                "{0} has id={1}, must be within 0–255",
                Cat,
                DiagnosticSeverity.Error,
                true
            );

            public static readonly DiagnosticDescriptor IdDuplicate = new(
                DiagnosticBands.RenderingIdDuplicate,
                "Duplicate command id",
                "{0} and {1} both use id=0x{2:X}",
                Cat,
                DiagnosticSeverity.Error,
                true
            );

            public static readonly DiagnosticDescriptor ExecutorSignature = new(
                DiagnosticBands.RenderingInvalidExecutorSignature,
                "Invalid executor signature",
                "{0}.Execute must be `static void Execute(ReadOnlySpan<byte>, ref CommandContext)` (executor of {1})",
                Cat,
                DiagnosticSeverity.Error,
                true
            );

            public static readonly DiagnosticDescriptor NotUnmanaged = new(
                DiagnosticBands.RenderingNotUnmanaged,
                "Command struct must be unmanaged",
                "{0} must be an unmanaged struct (no managed references) so its bytes can be dispatched",
                Cat,
                DiagnosticSeverity.Error,
                true
            );

            public static readonly DiagnosticDescriptor InconsistentCommandContext = new(
                DiagnosticBands.RenderingInconsistentCommandContext,
                "Inconsistent CommandContext type",
                "Executors use different CommandContext types: {0}",
                Cat,
                DiagnosticSeverity.Error,
                true
            );
        }
    }
}
