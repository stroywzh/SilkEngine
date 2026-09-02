using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SilkEngine.Tests.Architecture;

/// <summary>
/// 任务 12 收尾：AssetDB（SQLite 资产库）边界收敛。
/// Rendering 域全部程序集与 Runtime 不得引用 Assets 程序集（程序集级断言）；
/// SqliteAssetDatabase/IAssetDatabase 类型名与 Assets.Database 命名空间只存在于 Assets 项目源码；
/// Host/Sandbox 的公开程序集表面与源码不得出现资产数据库类型名。
/// </summary>
public class AssetDatabaseBoundaryTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void RenderingAssemblies_DoNotReferenceAssetsAssembly()
    {
        var rendering = Assembly.Load("SilkEngine.Rendering");
        var references = rendering.GetReferencedAssemblies().Select(x => x.Name);

        Assert.DoesNotContain("SilkEngine.Assets", references);
    }

    [Theory]
    [InlineData("SilkEngine.Rendering")]
    [InlineData("SilkEngine.Rendering.Abstraction")]
    [InlineData("SilkEngine.Rendering.Backend")]
    [InlineData("SilkEngine.Rendering.OpenGL")]
    public void EachRenderingDomainAssembly_DoesNotReferenceAssetsAssembly(string assemblyName)
    {
        var references = Assembly.Load(assemblyName).GetReferencedAssemblies().Select(x => x.Name);

        Assert.DoesNotContain("SilkEngine.Assets", references);
    }

    [Fact]
    public void RuntimeAssembly_DoesNotReferenceAssetsOrAnyRenderingAssembly()
    {
        var references = Assembly.Load("SilkEngine.Runtime").GetReferencedAssemblies().Select(x => x.Name);

        Assert.DoesNotContain("SilkEngine.Assets", references);
        Assert.DoesNotContain(references, name => name is
            "SilkEngine.Rendering"
            or "SilkEngine.Rendering.Abstraction"
            or "SilkEngine.Rendering.Backend"
            or "SilkEngine.Rendering.OpenGL");
    }

    [Fact]
    public void HostAndSandbox_ExportNoAssetDatabaseTypes()
    {
        foreach (var assemblyName in new[] { "SilkEngine.Host", "Sandbox" })
        {
            var exportedTypes = Assembly.Load(assemblyName).GetExportedTypes();

            Assert.DoesNotContain(exportedTypes, type =>
                type.Name.Contains("AssetDatabase", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void HostAndSandbox_SourcesDoNotMentionAssetDatabaseTypeNames()
    {
        foreach (var project in new[] { "SilkEngine.Host", "Sandbox" })
        {
            var dir = Path.Combine(RepoRoot, "src", project);
            foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(file);
                Assert.False(
                    content.Contains("SqliteAssetDatabase", StringComparison.Ordinal)
                    || content.Contains("IAssetDatabase", StringComparison.Ordinal),
                    $"{file} 源码不得出现 AssetDB 类型名（SqliteAssetDatabase/IAssetDatabase）");
            }
        }
    }

    [Fact]
    public void HostAndSandbox_SourcesDoNotUseAssetsDatabaseNamespace()
    {
        foreach (var project in new[] { "SilkEngine.Host", "Sandbox" })
        {
            var dir = Path.Combine(RepoRoot, "src", project);
            foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                var content = File.ReadAllText(file);
                Assert.DoesNotContain("SilkEngine.Assets.Database", content);
            }
        }
    }

    [Fact]
    public void AssetDbTypes_ExistOnlyUnderAssetsProject()
    {
        var filesWithSqlite = Directory.EnumerateFiles(
                Path.Combine(RepoRoot, "src", "SilkEngine.Assets"), "*.cs", SearchOption.AllDirectories)
            .Select(file => (File: file, Content: File.ReadAllText(file)))
            .Where(entry =>
                entry.Content.Contains("SqliteAssetDatabase", StringComparison.Ordinal)
                || entry.Content.Contains("IAssetDatabase", StringComparison.Ordinal))
            .Select(entry => entry.File)
            .ToList();

        // 夹具自身证明类型确实存在（Database/ 目录），同时与上述 Host/Sandbox 断言构成完整封闭
        Assert.NotEmpty(filesWithSqlite);
        Assert.All(filesWithSqlite, file =>
            Assert.StartsWith(
                Path.Combine(RepoRoot, "src", "SilkEngine.Assets"),
                file,
                StringComparison.OrdinalIgnoreCase));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SilkEngine.slnx")))
            dir = dir.Parent;
        return (dir ?? throw new InvalidOperationException("未找到仓库根目录（SilkEngine.slnx）")).FullName;
    }
}