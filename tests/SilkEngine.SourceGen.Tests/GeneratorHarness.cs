using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SilkEngine;
using SilkEngine.SourceGen;

namespace SilkEngine.SourceGen.Tests;

/// <summary>生成器测试驱动器：内存编译片段 → 运行生成器 → 返回生成源码与诊断。</summary>
internal static class GeneratorHarness
{
    private static List<PortableExecutableReference> BuildReferences()
    {
        var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p))
            .ToList();
        refs.Add(MetadataReference.CreateFromFile(typeof(Component).Assembly.Location));
        return refs;
    }

    public static (string[] Generated, ImmutableArray<Diagnostic> Diagnostics) Run(
        string source, string assemblyName = "SnapshotsAssembly")
    {
        var comp = CSharpCompilation.Create(
            assemblyName,
            new[] { SyntaxFactory.ParseSyntaxTree(source) },
            BuildReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new ComponentSerializerGenerator().AsSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(comp, out var outComp, out var diags);

        var generated = outComp.SyntaxTrees
            .Where(t => !comp.SyntaxTrees.Contains(t))
            .Select(t => Normalize(t.ToString()))
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();
        return (generated, diags);
    }

    /// <summary>生成代码回灌编译：验证生成产物自身无编译错误。</summary>
    public static ImmutableArray<Diagnostic> RunCompileCheck(
        string source, string assemblyName = "SnapshotsAssembly")
    {
        var comp = CSharpCompilation.Create(
            assemblyName,
            new[] { SyntaxFactory.ParseSyntaxTree(source) },
            BuildReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new ComponentSerializerGenerator().AsSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(comp, out var outComp, out _);
        return outComp.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();
    }

    public static string Normalize(string text) => text.Replace("\r\n", "\n").TrimEnd();
}
