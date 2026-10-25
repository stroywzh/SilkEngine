using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using SilkEngine.SourceGen.Diagnostics;

namespace SilkEngine.SourceGen.Mathematics
{
    /// <summary>
    /// Generates the mechanical skeleton of the math value types: the component indexer, exact
    /// equality, hashing and <see cref="System.IFormattable"/> formatting (proposal-01 §9 / D8).
    /// Mathematical operations, constants and conversions stay handwritten.
    /// </summary>
    /// <remarks>
    /// One partial file is emitted per annotated struct. The generated members keep the engine-wide
    /// contracts: exact <c>==</c>/<c>Equals</c>/<c>GetHashCode</c>, and formatting through the shared
    /// helpers with the declared prefix and default format.
    /// </remarks>
    [Generator(LanguageNames.CSharp)]
    public class MathValueSourceGen : IIncrementalGenerator
    {
        private const string AttrMetadataName = "SilkEngine.Mathematics.MathValueAttribute";
        private const string HashUtilName = "global::SilkEngine.Mathematics.Internal.HashUtil";
        private const string FormatUtilName = "global::SilkEngine.Mathematics.Internal.FormatUtil";

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var models = context
                .SyntaxProvider.ForAttributeWithMetadataName(
                    AttrMetadataName,
                    static (node, _) => node is StructDeclarationSyntax,
                    static (ctx, ct) => Extract(ctx, ct)
                )
                .Where(static m => m is not null)
                .Select(static (m, _) => m!);

            context.RegisterSourceOutput(models, static (spc, model) => Emit(spc, model));
        }

        private static MathValueModel? Extract(
            GeneratorAttributeSyntaxContext ctx,
            CancellationToken ct
        )
        {
            if (ctx.TargetSymbol is not INamedTypeSymbol structSym)
                return null;
            if (structSym.TypeKind != TypeKind.Struct)
                return null;

            var attr = ctx.Attributes.FirstOrDefault();
            if (attr is null)
                return null;

            var location = structSym.Locations.FirstOrDefault() ?? Location.None;
            var arguments = attr.ConstructorArguments;

            string? prefix = null;
            string? defaultFormat = null;
            var columns = 0;
            ImmutableArray<string> components = ImmutableArray<string>.Empty;

            if (arguments.Length == 3)
            {
                prefix = arguments[0].Value as string;
                defaultFormat = arguments[1].Value as string;
                components = ReadStringArray(arguments[2]);
            }
            else if (arguments.Length == 4)
            {
                prefix = arguments[0].Value as string;
                defaultFormat = arguments[1].Value as string;
                if (arguments[2].Value is int columnCount)
                    columns = columnCount;
                components = ReadStringArray(arguments[3]);
            }

            if (prefix is null || defaultFormat is null)
                return null;

            var componentTypes = new List<string>(components.Length);
            foreach (var component in components)
            {
                var field = structSym.GetMembers(component).OfType<IFieldSymbol>().FirstOrDefault();
                if (field is null)
                {
                    return new MathValueModel
                    {
                        TypeName = structSym.Name,
                        Location = location,
                        Error = component,
                    };
                }

                componentTypes.Add(DisplayType(field.Type));
            }

            if (componentTypes.Count == 0)
                return null;

            // MATH-Q-2: the generated indexer returns one element type (ComponentTypes[0]), so a struct
            // whose components differ in type cannot be generated faithfully; report SRC102 instead of
            // silently emitting an indexer that converts some components.
            string? mixedType = null;
            for (var i = 1; i < componentTypes.Count; i++)
            {
                if (!string.Equals(componentTypes[i], componentTypes[0], StringComparison.Ordinal))
                {
                    mixedType = componentTypes[i];
                    break;
                }
            }

            return new MathValueModel
            {
                TypeName = structSym.Name,
                Namespace =
                    structSym.ContainingNamespace is { IsGlobalNamespace: false }
                        ? structSym.ContainingNamespace.ToDisplayString()
                        : string.Empty,
                Prefix = prefix,
                DefaultFormat = defaultFormat,
                Columns = columns,
                Components = components,
                ComponentTypes = componentTypes.ToImmutableArray(),
                IsPublic = structSym.DeclaredAccessibility == Accessibility.Public,
                IsReadOnly = structSym.IsReadOnly,
                Location = location,
                MixedType = mixedType,
                PrimaryType = componentTypes[0],
            };
        }

        private static ImmutableArray<string> ReadStringArray(TypedConstant constant)
        {
            if (constant.Kind != TypedConstantKind.Array || constant.IsNull)
                return ImmutableArray<string>.Empty;

            var builder = ImmutableArray.CreateBuilder<string>();
            foreach (var value in constant.Values)
            {
                if (value.Value is string text)
                    builder.Add(text);
            }

            return builder.ToImmutable();
        }

        private static string DisplayType(ITypeSymbol type) =>
            type.SpecialType switch
            {
                SpecialType.System_Single => "float",
                SpecialType.System_Byte => "byte",
                SpecialType.System_Double => "double",
                SpecialType.System_Int32 => "int",
                _ => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            };

        private static void Emit(SourceProductionContext spc, MathValueModel model)
        {
            if (model.Error is not null)
            {
                spc.ReportDiagnostic(
                    Diagnostic.Create(
                        Diags.InvalidComponent,
                        model.Location ?? Location.None,
                        model.TypeName,
                        model.Error
                    )
                );
                return;
            }

            if (model.MixedType is not null)
            {
                // MATH-Q-2: fail loudly instead of generating a lossy indexer.
                spc.ReportDiagnostic(
                    Diagnostic.Create(
                        Diags.MixedComponentTypes,
                        model.Location ?? Location.None,
                        model.TypeName,
                        model.MixedType,
                        model.PrimaryType
                    )
                );
                return;
            }

            spc.AddSource(
                model.TypeName + ".MathValue.g.cs",
                SourceText.From(Build(model), Encoding.UTF8)
            );
        }

        private static string Build(MathValueModel model)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");

            if (model.Namespace.Length > 0)
            {
                sb.Append("namespace ").Append(model.Namespace).AppendLine(";");
                sb.AppendLine();
            }

            sb.Append(model.IsPublic ? "public" : "internal").Append(' ');
            if (model.IsReadOnly)
                sb.Append("readonly ");
            sb.Append("partial struct ").Append(model.TypeName).AppendLine();
            sb.AppendLine("{");

            AppendIndexer(sb, model);
            AppendEquality(sb, model);
            AppendFormatting(sb, model);

            sb.AppendLine("}");
            return sb.ToString();
        }

        private static void AppendIndexer(StringBuilder sb, MathValueModel model)
        {
            var element = model.ComponentTypes[0];
            sb.AppendLine("    /// <summary>Gets a component by index (generated).</summary>");

            if (model.Columns <= 0)
            {
                sb.Append("    public ").Append(element).AppendLine(" this[int i] =>");
                sb.AppendLine("        i switch");
                sb.AppendLine("        {");
                for (var i = 0; i < model.Components.Length; i++)
                {
                    sb.Append("            ").Append(i).Append(" => ").Append(model.Components[i]).AppendLine(",");
                }
                sb.AppendLine("            _ => throw new global::System.IndexOutOfRangeException(),");
                sb.AppendLine("        };");
                sb.AppendLine();
                return;
            }

            var columns = model.Columns;
            var rows = model.Components.Length / columns;
            sb.Append("    public ").Append(element).AppendLine(" this[int row, int col] =>");
            sb.AppendLine("        (row, col) switch");
            sb.AppendLine("        {");
            for (var row = 0; row < rows; row++)
            {
                for (var col = 0; col < columns; col++)
                {
                    var field = model.Components[row * columns + col];
                    sb.Append("            (").Append(row).Append(", ").Append(col).Append(") => ").Append(field).AppendLine(",");
                }
            }
            sb.AppendLine("            _ => throw new global::System.IndexOutOfRangeException(),");
            sb.AppendLine("        };");
            sb.AppendLine();
        }

        private static void AppendEquality(StringBuilder sb, MathValueModel model)
        {
            var type = model.TypeName;
            var comparisons = string.Join(
                " && ",
                model.Components.Select(c => "lhs." + c + " == rhs." + c)
            );

            sb.AppendLine("    /// <summary>Exact component-wise equality (generated).</summary>");
            sb.Append("    public static bool operator ==(").Append(type).Append(" lhs, ").Append(type).AppendLine(" rhs) =>");
            sb.Append("        ").Append(comparisons).AppendLine(";");
            sb.AppendLine();
            sb.Append("    public static bool operator !=(").Append(type).Append(" lhs, ").Append(type).AppendLine(" rhs) => !(lhs == rhs);");
            sb.AppendLine();
            sb.Append("    public bool Equals(").Append(type).AppendLine(" other) => this == other;");
            sb.AppendLine();
            sb.Append("    public override bool Equals(object? obj) => obj is ").Append(type).AppendLine(" other && this == other;");
            sb.AppendLine();

            AppendHashCode(sb, model);
            sb.AppendLine();
        }

        private static void AppendHashCode(StringBuilder sb, MathValueModel model)
        {
            var components = model.Components;
            if (components.Length <= 4)
            {
                sb.Append("    public override int GetHashCode() => ")
                    .Append(HashUtilName)
                    .Append(".Of(")
                    .Append(string.Join(", ", components))
                    .AppendLine(");");
                return;
            }

            sb.AppendLine("    public override int GetHashCode()");
            sb.AppendLine("    {");
            sb.AppendLine("        unchecked");
            sb.AppendLine("        {");
            var first = true;
            for (var start = 0; start < components.Length; start += 4)
            {
                var group = string.Join(", ", components.Skip(start).Take(4));
                if (first)
                {
                    sb.Append("            int h = ").Append(HashUtilName).Append(".Of(").Append(group).AppendLine(");");
                    first = false;
                }
                else
                {
                    sb.Append("            h = h * 397 ^ ").Append(HashUtilName).Append(".Of(").Append(group).AppendLine(");");
                }
            }
            sb.AppendLine("            return h;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
        }

        private static void AppendFormatting(StringBuilder sb, MathValueModel model)
        {
            sb.AppendLine("    /// <summary>Formats this value (generated).</summary>");
            sb.AppendLine("    public override string ToString() => ToString(null, null);");
            sb.AppendLine();
            sb.AppendLine("    public string ToString(string format) => ToString(format, null);");
            sb.AppendLine();
            sb.Append("    public string ToString(string? format, global::System.IFormatProvider? formatProvider) =>");
            sb.AppendLine();
            sb.Append("        ")
                .Append(FormatUtilName)
                .Append(".Format(")
                .Append('"').Append(model.Prefix).Append("\", \"")
                .Append(model.DefaultFormat)
                .Append("\", format, formatProvider, ")
                .Append(string.Join(", ", model.Components))
                .AppendLine(");");
        }

        internal sealed record MathValueModel
        {
            public string TypeName { get; init; } = "";
            public string Namespace { get; init; } = "";
            public string Prefix { get; init; } = "";
            public string DefaultFormat { get; init; } = "";
            public int Columns { get; init; }
            public ImmutableArray<string> Components { get; init; } = ImmutableArray<string>.Empty;
            public ImmutableArray<string> ComponentTypes { get; init; } = ImmutableArray<string>.Empty;
            public bool IsPublic { get; init; }
            public bool IsReadOnly { get; init; }
            public Location? Location { get; init; }
            public string? Error { get; init; }

            /// <summary>First component type used by the generated indexer (MATH-Q-2).</summary>
            public string? PrimaryType { get; init; }

            /// <summary>First component type that differs from <see cref="PrimaryType"/>, if any.</summary>
            public string? MixedType { get; init; }
        }

        internal static class Diags
        {
            private const string Cat = "SilkEngine.Math";

            public static readonly DiagnosticDescriptor InvalidComponent = new(
                DiagnosticBands.MathematicsInvalidComponent,
                "MathValue component is not a field",
                "{0}.{1} must be a field of the annotated struct",
                Cat,
                DiagnosticSeverity.Error,
                true
            );

            public static readonly DiagnosticDescriptor MixedComponentTypes = new(
                DiagnosticBands.MathematicsMixedComponentTypes,
                "MathValue components must share one type",
                "{0} mixes component types: {1} differs from {2}",
                Cat,
                DiagnosticSeverity.Error,
                true
            );
        }
    }
}
