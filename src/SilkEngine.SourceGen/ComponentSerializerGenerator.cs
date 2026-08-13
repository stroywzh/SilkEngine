using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SilkEngine.SourceGen;

/// <summary>
/// 序列化源生成器：对顶层 Component 子类生成 partial override WriteTo/ReadFrom。
/// 规则落实 DESIGN §2.2/2.3 与计划附录规则表 R0-R8：
/// 默认全字段（public/private；编译器隐式字段如自动属性后备字段跳过）；[NoSerializeField] 跳过；白名单原生 get/set（缺失键保留当前值）；
/// 资产引用经 AssetRefCodec（属性感知：字段 _x ↔ 公共属性 X 时以属性为访问器、键用属性名）；
/// 同程序集类型递归展开为 "Field_Sub" 平面键；外部程序集类型 STJ 兜底（SetRaw/GetRaw）；
/// [SerializableInternal] 违规（SENG001-004）编译错误。
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ComponentSerializerGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor Seng001 = new(
        id: "SENG001",
        title: "SerializableInternal 禁止外部使用",
        messageFormat: "特性 [SerializableInternal] 仅允许在 SilkEngine 程序集内使用（当前程序集 '{0}'）",
        category: "SilkEngine.Serialization",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Seng002 = new(
        id: "SENG002",
        title: "SerializableInternal 类型存在白名单外字段",
        messageFormat: "类型 '{0}' 的字段 '{1}' 不在序列化白名单内且未标记 [NoSerializeField]",
        category: "SilkEngine.Serialization",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Seng003 = new(
        id: "SENG003",
        title: "SerializableInternal 类型未登记",
        messageFormat: "类型 '{0}' 未调用 ComponentTypeRegistry.Register<T>() 登记，无法反序列化",
        category: "SilkEngine.Serialization",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Seng004 = new(
        id: "SENG004",
        title: "SerializableInternal 仅可用于 Component 子类",
        messageFormat: "类型 '{0}' 不是 Component 子类，不能标记 [SerializableInternal]",
        category: "SilkEngine.Serialization",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>全限定名格式（不含特殊类型关键字：float→global::System.Single，string[]→global::System.String[]）。</summary>
    private static readonly SymbolDisplayFormat NoSpecialTypesFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var components = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, ct) => ComponentInfo.TryCreate(ctx, ct))
            .Where(static info => info is not null)
            .Collect();
        context.RegisterSourceOutput(components, static (spc, infos) =>
        {
            foreach (var info in infos)
            {
                if (info is { } i)
                    i.Emit(spc);
            }
        });

        var registered = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is InvocationExpressionSyntax inv
                    && inv.ArgumentList.Arguments.Count == 0
                    && inv.Expression is MemberAccessExpressionSyntax ma
                    && ma.Name is GenericNameSyntax
                    && ma.Name.Identifier.Text == "Register"
                    || node is InvocationExpressionSyntax inv2
                    && inv2.ArgumentList.Arguments.Count == 0
                    && inv2.Expression is GenericNameSyntax g
                    && g.Identifier.Text == "Register",
                static (ctx, ct) => TryGetRegisteredType(ctx, ct))
            .Where(static t => t is not null)
            .Collect();

        var marked = context.SyntaxProvider.ForAttributeWithMetadataName(
            GenConstants.SerializableInternalAttribute,
            static (_, _) => true,
            static (ctx, ct) =>
            {
                if (ctx.TargetSymbol is not INamedTypeSymbol symbol)
                    return null;
                var component = ctx.SemanticModel.Compilation.GetTypeByMetadataName(GenConstants.Component);
                return new MarkedItem(symbol, component is not null && IsDerivedFrom(symbol, component));
            })
            .Where(static m => m is not null)
            .Collect();

        context.RegisterSourceOutput(marked.Combine(registered), static (spc, pair) =>
        {
            var registeredTypes = new HashSet<INamedTypeSymbol>(
                pair.Right.Where(t => t is not null).Cast<INamedTypeSymbol>(),
                SymbolEqualityComparer.Default);
            foreach (var item in pair.Left)
            {
                if (item is { } i)
                    Validate(spc, i, registeredTypes);
            }
        });
    }

    // ===== 注册表扫描（SENG003） =====

    private static INamedTypeSymbol? TryGetRegisteredType(GeneratorSyntaxContext ctx, CancellationToken ct)
    {
        if (ctx.Node is not InvocationExpressionSyntax inv)
            return null;
        if (ctx.SemanticModel.GetSymbolInfo(inv, ct).Symbol is not IMethodSymbol method)
            return null;
        if (method.Name != "Register" || method.TypeArguments.Length != 1)
            return null;
        if (method.ContainingType?.ToDisplayString() != GenConstants.ComponentTypeRegistry)
            return null;
        return method.TypeArguments[0] as INamedTypeSymbol;
    }

    // ===== [SerializableInternal] 校验（SENG001-004） =====

    private static void Validate(SourceProductionContext spc, MarkedItem item,
        HashSet<INamedTypeSymbol> registeredTypes)
    {
        var type = item.Symbol;
        var loc = type.Locations.FirstOrDefault() ?? Location.None;
        if (type.ContainingAssembly?.Name != "SilkEngine")
        {
            spc.ReportDiagnostic(Diagnostic.Create(Seng001, loc, type.ContainingAssembly?.Name ?? "?"));
            return;
        }
        if (!item.IsComponent)
        {
            spc.ReportDiagnostic(Diagnostic.Create(Seng004, loc, type.Name));
            return;
        }
        if (!registeredTypes.Contains(type))
            spc.ReportDiagnostic(Diagnostic.Create(Seng003, loc, type.Name));
        foreach (var f in type.GetMembers().OfType<IFieldSymbol>())
        {
            if (f.IsStatic || f.IsConst || f.IsReadOnly || f.IsImplicitlyDeclared)
                continue;
            if (HasAttribute(f, GenConstants.NoSerializeFieldAttribute))
                continue;
            if (!IsWhitelist(f.Type))
                spc.ReportDiagnostic(Diagnostic.Create(Seng002, f.Locations.FirstOrDefault() ?? loc, type.Name, f.Name));
        }
    }

    // ===== 类型分类 =====

    private static bool IsWhitelist(ITypeSymbol t) => ScalarOf(t) is not null || IsAsset(t);

    private static ScalarInfo? ScalarOf(ITypeSymbol t)
    {
        switch (t.ToDisplayString(NoSpecialTypesFormat))
        {
            case GenConstants.Int32: return new ScalarInfo("SetInt", "GetInt", IsGuid: false);
            case GenConstants.Single: return new ScalarInfo("SetFloat", "GetFloat", IsGuid: false);
            case GenConstants.Boolean: return new ScalarInfo("SetBool", "GetBool", IsGuid: false);
            case GenConstants.String: return new ScalarInfo("SetString", "GetString", IsGuid: false);
            case GenConstants.Guid: return new ScalarInfo("SetString", "GetString", IsGuid: true);
            case GenConstants.Vector3: return new ScalarInfo("SetVector3", "GetVector3", IsGuid: false);
            case GenConstants.Quaternion: return new ScalarInfo("SetQuaternion", "GetQuaternion", IsGuid: false);
            default: return null;
        }
    }

    private static bool IsAsset(ITypeSymbol t)
    {
        switch (t.ToDisplayString(NoSpecialTypesFormat))
        {
            case GenConstants.Shader:
            case GenConstants.Mesh:
            case GenConstants.Material:
            case GenConstants.Texture2D:
                return true;
            default:
                return false;
        }
    }

    private static string Global(ITypeSymbol t) => t.ToDisplayString(NoSpecialTypesFormat);

    private static bool IsDerivedFrom(INamedTypeSymbol symbol, INamedTypeSymbol baseType)
    {
        for (var t = symbol.BaseType; t is not null; t = t.BaseType)
            if (SymbolEqualityComparer.Default.Equals(t, baseType))
                return true;
        return false;
    }

    private static IPropertySymbol? FindAccessorProperty(INamedTypeSymbol type, IFieldSymbol field)
    {
        var baseName = field.Name.TrimStart('_');
        foreach (var m in type.GetMembers())
        {
            if (m is IPropertySymbol p
                && p.Name.Equals(baseName, StringComparison.OrdinalIgnoreCase)
                && p.GetMethod?.DeclaredAccessibility == Accessibility.Public
                && p.SetMethod?.DeclaredAccessibility == Accessibility.Public)
                return p;
        }
        return null;
    }

    /// <summary>
    /// 递归展开判定：同程序集命名类型、非数组、展开栈无环、未手动接管 ReadFrom/WriteTo、
    /// 引用类型需公共无参构造（生成代码 ??= new 用）。不满足 → STJ 兜底。
    /// </summary>
    private static bool CanExpand(ITypeSymbol t, IAssemblySymbol componentAssembly,
        HashSet<INamedTypeSymbol> stack, out string? reason)
    {
        reason = null;
        if (t is not INamedTypeSymbol named || named.TypeKind == TypeKind.Array)
            return false;
        if (!SymbolEqualityComparer.Default.Equals(named.ContainingAssembly, componentAssembly))
            return false;   // 外部程序集 → STJ
        if (stack.Contains(named))
            return false;   // 环 → STJ
        if (named.GetMembers("ReadFrom").Length > 0 || named.GetMembers("WriteTo").Length > 0)
            return false;   // 手动接管 → STJ
        if (named.IsReferenceType
            && !named.InstanceConstructors.Any(c => c.Parameters.Length == 0 && c.DeclaredAccessibility == Accessibility.Public))
            return false;   // 无公共无参构造 → STJ
        return true;
    }

    // ===== 字段规划与生成 =====

    private enum FieldKind { Scalar, Asset, Stj }

    private sealed class Leaf
    {
        public required string Key;
        public required FieldKind Kind;
        public required string Access;
        public required List<string> Guards;    // 写路径空值守卫（"A.B != null"）
        public required List<string> Ensures;   // 读路径实例化（"A.B ??= new T();"）
        public string? WriteMethod;
        public string? GetMethod;
        public bool IsGuid;
        public string? TargetType;              // Asset/STJ 泛型参数（global:: 全名）
        public bool IsAssetProperty;
        public bool RefType;
    }

    private sealed record ScalarInfo(string WriteMethod, string GetMethod, bool IsGuid);

    private sealed record MarkedItem(INamedTypeSymbol Symbol, bool IsComponent);

    private sealed class ComponentInfo
    {
        public required INamedTypeSymbol Symbol { get; init; }

        public string HintName => Symbol.ToDisplayString()
            .Replace("global::", "").Replace('.', '_') + ".g.cs";

        public static ComponentInfo? TryCreate(GeneratorSyntaxContext ctx, CancellationToken ct)
        {
            if (ctx.Node is not ClassDeclarationSyntax)
                return null;
            if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node, ct) is not INamedTypeSymbol symbol)
                return null;
            if (symbol.IsAbstract || symbol.IsStatic)
                return null;
            if (symbol.ContainingType is not null)
                return null;   // 嵌套类型不生成（附录规则 R0）
            var component = ctx.SemanticModel.Compilation.GetTypeByMetadataName(GenConstants.Component);
            if (component is null || !IsDerivedFrom(symbol, component))
                return null;
            if (symbol.GetMembers("ReadFrom").Length > 0 || symbol.GetMembers("WriteTo").Length > 0)
                return null;   // 用户手动接管（附录规则 R1）
            return new ComponentInfo { Symbol = symbol };
        }

        public void Emit(SourceProductionContext spc)
        {
            var leaves = new List<Leaf>();
            var stack = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default) { Symbol };
            BuildLeaves(Symbol, access: "", key: "", new List<string>(), new List<string>(), stack, leaves);

            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("// 由 SilkEngine.SourceGen.ComponentSerializerGenerator 生成");
            sb.AppendLine();
            var ns = Symbol.ContainingNamespace;
            if (!ns.IsGlobalNamespace)
            {
                sb.Append("namespace ").Append(ns.ToDisplayString()).AppendLine(";");
                sb.AppendLine();
            }
            sb.Append("partial class ").Append(Symbol.Name).AppendLine();
            sb.AppendLine("{");
            sb.AppendLine("    public override void WriteTo(global::" + GenConstants.SerializedNode + " node)");
            sb.AppendLine("    {");
            foreach (var leaf in leaves)
                EmitWrite(sb, leaf);
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public override void ReadFrom(global::" + GenConstants.SerializedNode + " node)");
            sb.AppendLine("    {");
            foreach (var leaf in leaves)
                EmitRead(sb, leaf);
            sb.AppendLine("    }");
            sb.AppendLine("}");
            spc.AddSource(HintName, SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        private static void BuildLeaves(
            INamedTypeSymbol type,
            string access,
            string key,
            List<string> guards,
            List<string> ensures,
            HashSet<INamedTypeSymbol> stack,
            List<Leaf> leaves)
        {
            foreach (var f in type.GetMembers().OfType<IFieldSymbol>())
            {
                if (f.IsStatic || f.IsConst || f.IsReadOnly || f.IsImplicitlyDeclared)
                    continue;
                if (HasAttribute(f, GenConstants.NoSerializeFieldAttribute))
                    continue;
                var childAccess = access.Length == 0 ? f.Name : access + "." + f.Name;
                var childKey = key.Length == 0 ? f.Name : key + "_" + f.Name;

                var scalar = ScalarOf(f.Type);
                if (scalar is not null)
                {
                    leaves.Add(new Leaf
                    {
                        Key = childKey, Kind = FieldKind.Scalar, Access = childAccess,
                        Guards = guards, Ensures = ensures,
                        WriteMethod = scalar.WriteMethod, GetMethod = scalar.GetMethod, IsGuid = scalar.IsGuid,
                    });
                    continue;
                }
                if (IsAsset(f.Type))
                {
                    var property = FindAccessorProperty(type, f);
                    leaves.Add(new Leaf
                    {
                        Key = property is null ? childKey : property.Name,
                        Kind = FieldKind.Asset,
                        Access = property is null ? childAccess : property.Name,
                        Guards = guards, Ensures = ensures,
                        TargetType = Global(f.Type), IsAssetProperty = property is not null,
                    });
                    continue;
                }
                if (CanExpand(f.Type, type.ContainingAssembly, stack, out _))
                {
                    var g = new List<string>(guards);
                    var e = new List<string>(ensures);
                    if (f.Type.IsReferenceType)
                    {
                        g.Add(childAccess + " != null");
                        e.Add(childAccess + " ??= new " + Global(f.Type) + "();");
                    }
                    stack.Add((INamedTypeSymbol)f.Type);
                    BuildLeaves((INamedTypeSymbol)f.Type, childAccess, childKey, g, e, stack, leaves);
                    stack.Remove((INamedTypeSymbol)f.Type);
                    continue;
                }
                leaves.Add(new Leaf
                {
                    Key = childKey, Kind = FieldKind.Stj, Access = childAccess,
                    Guards = guards, Ensures = ensures,
                    TargetType = Global(f.Type), RefType = f.Type.IsReferenceType,
                });
            }
        }

        private static void EmitWrite(StringBuilder sb, Leaf leaf)
        {
            var guard = leaf.Guards.Count > 0 ? "if (" + string.Join(" && ", leaf.Guards) + ") " : "";
            switch (leaf.Kind)
            {
                case FieldKind.Scalar:
                    if (leaf.IsGuid)
                        sb.AppendLine("        " + guard + "node.SetString(\"" + leaf.Key + "\", "
                            + leaf.Access + " == default ? null : " + leaf.Access + ".ToString());");
                    else
                        sb.AppendLine("        " + guard + "node." + leaf.WriteMethod + "(\"" + leaf.Key
                            + "\", " + leaf.Access + ");");
                    break;
                case FieldKind.Asset:
                    sb.AppendLine("        " + guard + "global::" + GenConstants.AssetRefCodec
                        + ".Write(node, \"" + leaf.Key + "\", " + leaf.Access + ");");
                    break;
                case FieldKind.Stj:
                    if (leaf.RefType)
                        sb.AppendLine("        " + guard + "node.SetRaw(\"" + leaf.Key + "\", " + leaf.Access
                            + " == null ? null : global::System.Text.Json.JsonSerializer.SerializeToNode(" + leaf.Access + "));");
                    else
                        sb.AppendLine("        " + guard + "node.SetRaw(\"" + leaf.Key
                            + "\", global::System.Text.Json.JsonSerializer.SerializeToNode(" + leaf.Access + "));");
                    break;
            }
        }

        private static void EmitRead(StringBuilder sb, Leaf leaf)
        {
            switch (leaf.Kind)
            {
                case FieldKind.Scalar:
                    if (leaf.IsGuid)
                    {
                        sb.AppendLine("        if (node.ContainsKey(\"" + leaf.Key + "\"))");
                        sb.AppendLine("        {");
                        foreach (var e in leaf.Ensures)
                            sb.AppendLine("            " + e);
                        sb.AppendLine("            var __v = node.GetString(\"" + leaf.Key + "\");");
                        sb.AppendLine("            " + leaf.Access
                            + " = __v is null || !global::System.Guid.TryParse(__v, out var __g) ? default : __g;");
                        sb.AppendLine("        }");
                    }
                    else if (leaf.Ensures.Count > 0)
                    {
                        sb.AppendLine("        if (node.ContainsKey(\"" + leaf.Key + "\"))");
                        sb.AppendLine("        {");
                        foreach (var e in leaf.Ensures)
                            sb.AppendLine("            " + e);
                        sb.AppendLine("            " + leaf.Access + " = node." + leaf.GetMethod
                            + "(\"" + leaf.Key + "\");");
                        sb.AppendLine("        }");
                    }
                    else
                    {
                        sb.AppendLine("        if (node.ContainsKey(\"" + leaf.Key + "\")) " + leaf.Access
                            + " = node." + leaf.GetMethod + "(\"" + leaf.Key + "\");");
                    }
                    break;
                case FieldKind.Asset:
                    if (leaf.IsAssetProperty)
                        sb.AppendLine("        " + leaf.Access + " = global::" + GenConstants.AssetRefCodec
                            + ".Read<" + leaf.TargetType + ">(node, \"" + leaf.Key + "\");");
                    else
                        sb.AppendLine("        global::" + GenConstants.AssetRefCodec
                            + ".ReadTracked<" + leaf.TargetType + ">(ref " + leaf.Access
                            + ", node, \"" + leaf.Key + "\");");
                    break;
                case FieldKind.Stj:
                    var rawName = "__raw" + leaf.Key.Replace(".", "_");
                    sb.AppendLine("        var " + rawName + " = node.GetRaw(\"" + leaf.Key + "\");");
                    sb.AppendLine("        if (" + rawName + " != null)");
                    sb.AppendLine("        {");
                    foreach (var e in leaf.Ensures)
                        sb.AppendLine("            " + e);
                    sb.AppendLine("            try { " + leaf.Access
                        + " = global::System.Text.Json.JsonSerializer.Deserialize<" + leaf.TargetType
                        + ">(" + rawName + "); }");
                    sb.AppendLine("            catch (global::System.Text.Json.JsonException) { }");
                    sb.AppendLine("        }");
                    break;
            }
        }
    }

    private static bool HasAttribute(ISymbol symbol, string metadataName)
    {
        foreach (var a in symbol.GetAttributes())
            if (a.AttributeClass?.ToDisplayString() == metadataName)
                return true;
        return false;
    }
}
