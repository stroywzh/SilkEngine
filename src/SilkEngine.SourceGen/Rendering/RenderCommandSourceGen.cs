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

namespace SilkEngine.SourceGen.Rendering
{
    [Generator(LanguageNames.CSharp)]
    public class RenderCommandSourceGen : IIncrementalGenerator
    {
        private const string AttrMetadataName =
            "SilkEngine.Rendering.Attributes.RenderCommandAttribute";
        private const string TargetNamespace = "SilkEngine.Rendering";

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

            context.RegisterSourceOutput(commands, static (spc, arr) => Emit(spc, arr));
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
            // if (!TryGetExecutorSignature(executorSym, out ctxFullName))
            //     diags.Add(
            //         Diagnostic.Create(
            //             Diags.ExecutorSignature,
            //             loc,
            //             executorSym.Name,
            //             structSym.Name
            //         )
            //     );
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
                ExecutorFullName = execFullName,
                CommandContextFullName = ctxFullName,
                Id = id,
                Loc = loc,
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
        private static void Emit(SourceProductionContext spc, ImmutableArray<CommandModel> models)
        {
            foreach (var m in models)
            foreach (var d in m.Diagnostics)
                spc.ReportDiagnostic(d);

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
                    spc.ReportDiagnostic(
                        Diagnostic.Create(
                            Diags.IdDuplicate,
                            dup.Loc ?? Location.None, // ← 不是 dup.Locations
                            dup.TypeName,
                            first,
                            dup.Id
                        )
                    );
            }

            // ③ 计算 valid：errorFree 减去 duplicateIds
            var valid = errorFree
                .Where(m => m.CommandContextFullName is not null)
                .Where(m => !duplicateIds.Contains(m.Id))
                .OrderBy(m => m.Id)
                .ToList();

            if (valid.Count == 0)
                return;

            // ④ ctxFullName 一致性
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
                return;
            }

            spc.AddSource(
                "RenderCommandDispatcher.g.cs",
                EmitDispatcher(valid, ctxNames[0])
            );

            spc.AddSource(
                "RenderCommandExtensions.g.cs",
                EmitExtensions(valid)
            );
        }

        private static SourceText EmitExtensions(List<CommandModel> cmds)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("namespace " + TargetNamespace + ";");
            sb.AppendLine();
            sb.AppendLine(
                "/// <summary>Strongly typed recording helpers generated from [RenderCommand] declarations.</summary>"
            );
            sb.AppendLine("public static class RenderCommandExtensions");
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

        private static SourceText EmitDispatcher(List<CommandModel> cmds, string ctxFullName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine("namespace " + TargetNamespace + ";");
            sb.AppendLine();
            sb.AppendLine("internal static class RenderCommandDispatcher_Generated");
            sb.AppendLine("{");
            sb.Append(
                    "    public static void Dispatch(int id, global::System.ReadOnlySpan<byte> param, ref "
                )
                .Append(ctxFullName)
                .AppendLine(" ctx)");
            sb.AppendLine("    {");
            sb.AppendLine("        switch (id)");
            sb.AppendLine("        {");
            foreach (var c in cmds)
                sb.Append("            case ")
                    .Append(c.Id)
                    .Append(": ")
                    .Append(c.ExecutorFullName)
                    .AppendLine(".Execute(param, ref ctx); break;");
            sb.AppendLine(
                "            default: throw new global::System.InvalidOperationException($\"Unknown command id 0x{id:X}\");"
            );
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return SourceText.From(sb.ToString(), Encoding.UTF8);
        }

        // ───────────────────────── 模型 & 诊断 ─────────────────────────
        internal sealed record CommandModel
        {
            public string TypeName { get; init; } = "";
            public string CommandFullName { get; init; } = "";
            public string ExecutorFullName { get; init; } = "";
            public string? CommandContextFullName { get; init; }
            public int Id { get; init; }
            public Location? Loc { get; init; } // ← 新增
            public ImmutableArray<Diagnostic> Diagnostics { get; init; }
            public string DiagnosticsSignature { get; init; } = "";

            public bool Equals(CommandModel? other) =>
                other is not null
                && CommandFullName == other.CommandFullName
                && ExecutorFullName == other.ExecutorFullName
                && CommandContextFullName == other.CommandContextFullName
                && Id == other.Id
                && DiagnosticsSignature == other.DiagnosticsSignature; // Loc 不参与

            public override int GetHashCode() =>
                HashCode.Combine(
                    CommandFullName,
                    ExecutorFullName,
                    CommandContextFullName,
                    Id,
                    DiagnosticsSignature
                );
        }

        internal static class Diags
        {
            private const string Cat = "SilkEngine.Render";

            public static readonly DiagnosticDescriptor IdOutOfRange = new(
                "SRC001",
                "Command id must be 0–255",
                "{0} has id={1}, must be within 0–255",
                Cat,
                DiagnosticSeverity.Error,
                true
            );

            public static readonly DiagnosticDescriptor IdDuplicate = new(
                "SRC002",
                "Duplicate command id",
                "{0} and {1} both use id=0x{2:X}",
                Cat,
                DiagnosticSeverity.Error,
                true
            );

            public static readonly DiagnosticDescriptor ExecutorSignature = new(
                "SRC003",
                "Invalid executor signature",
                "{0}.Execute must be `static void Execute(ReadOnlySpan<byte>, ref CommandContext)` (declared on {1})",
                Cat,
                DiagnosticSeverity.Error,
                true
            );

            public static readonly DiagnosticDescriptor NotUnmanaged = new(
                "SRC006",
                "Command struct must be unmanaged",
                "{0} must be an unmanaged struct (no managed references) so its bytes can be dispatched",
                Cat,
                DiagnosticSeverity.Error,
                true
            );

            public static readonly DiagnosticDescriptor InconsistentCommandContext = new(
                "SRC004",
                "Inconsistent CommandContext type",
                "Executors use different CommandContext types: {0}",
                Cat,
                DiagnosticSeverity.Error,
                true
            );
        }
    }
}
