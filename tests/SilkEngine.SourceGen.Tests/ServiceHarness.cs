using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SilkEngine.Scene;
using SilkEngine.SourceGen;

namespace SilkEngine.SourceGen.Tests;

/// <summary>ServiceRegistrationGenerator 测试驱动器：内存编译片段 → 运行生成器 → 返回生成源码与诊断。</summary>
internal static class ServiceHarness
{
    internal static List<PortableExecutableReference> References { get; } = BuildReferences();

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
        string source, string assemblyName = "ServiceAssembly")
    {
        var comp = CSharpCompilation.Create(
            assemblyName,
            new[] { SyntaxFactory.ParseSyntaxTree(source) },
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var driver = CSharpGeneratorDriver.Create(new ServiceRegistrationGenerator().AsSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(comp, out var outComp, out var diags);
        var generated = outComp.SyntaxTrees
            .Where(t => !comp.SyntaxTrees.Contains(t))
            .Select(t => Normalize(t.ToString()))
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();
        return (generated, diags);
    }

    public static ImmutableArray<Diagnostic> RunCompileCheck(string source, string assemblyName = "ServiceAssembly")
    {
        var comp = CSharpCompilation.Create(
            assemblyName,
            new[] { SyntaxFactory.ParseSyntaxTree(source) },
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var driver = CSharpGeneratorDriver.Create(new ServiceRegistrationGenerator().AsSourceGenerator());
        driver.RunGeneratorsAndUpdateCompilation(comp, out var outComp, out _);
        return outComp.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();
    }

    /// <summary>生成代码规范化（换行符统一）。</summary>
    public static string Normalize(string text) => text.Replace("\r\n", "\n").TrimEnd();
}
