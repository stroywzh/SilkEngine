using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SilkEngine.Tests.Architecture;

/// <summary>
/// 程序集依赖方向边界（architecture-convergence 任务 4）：
/// 契约层（Rendering.Abstraction/Backend）无资产语义；Assets/Scene/Rendering 只依赖契约与 Runtime；
/// Threading 不依赖 Rendering/Assets；Sandbox 仅直接引用 Host 组合根。
/// </summary>
public class DependencyBoundaryTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Fact]
    public void ProjectReferences_MatchTargetDependencyDirection()
    {
        AssertProjectReferences("SilkEngine.Assets", "SilkEngine.Rendering.Abstraction");
        AssertProjectReferences("SilkEngine.Rendering", "SilkEngine.Rendering.Abstraction");
        AssertProjectReferences("SilkEngine.Rendering", "SilkEngine.Rendering.Backend");
        AssertProjectReferences("SilkEngine.Rendering.OpenGL", "SilkEngine.Rendering.Backend");
        AssertProjectReferences("SilkEngine.Rendering.OpenGL", "SilkEngine.Rendering");
        AssertProjectReferences("SilkEngine.Scene", "SilkEngine.Rendering.Abstraction");
        AssertProjectReferences("SilkEngine.Scene", "SilkEngine.Assets");
        AssertProjectReferences("SilkEngine.Host", "SilkEngine.Runtime");
        AssertProjectReferences("Sandbox", "SilkEngine.Host");

        AssertProjectDoesNotReference("SilkEngine.Runtime", "SilkEngine.Assets");
        AssertProjectDoesNotReference("SilkEngine.Runtime", "SilkEngine.Rendering");
        AssertProjectDoesNotReference("SilkEngine.Runtime", "SilkEngine.Rendering.Abstraction");
        AssertProjectDoesNotReference("SilkEngine.Rendering", "SilkEngine.Assets");
        AssertProjectDoesNotReference("SilkEngine.Rendering", "SilkEngine.Scene");
        AssertProjectDoesNotReference("SilkEngine.Threading", "SilkEngine.Rendering");
        AssertProjectDoesNotReference("SilkEngine.Threading", "SilkEngine.Assets");
        AssertProjectDoesNotReference("SilkEngine.Rendering.OpenGL", "SilkEngine.Assets");
        AssertProjectDoesNotReference("SilkEngine.Rendering.OpenGL", "SilkEngine.Scene");
        AssertProjectDoesNotReference("SilkEngine.Rendering.Abstraction", "SilkEngine.Assets");
        AssertProjectDoesNotReference("SilkEngine.Rendering.Backend", "SilkEngine.Assets");
        AssertProjectDoesNotReference("Sandbox", "SilkEngine.Runtime");
        AssertProjectDoesNotReference("Sandbox", "SilkEngine.Assets");
        AssertProjectDoesNotReference("Sandbox", "SilkEngine.Scene");
        AssertProjectDoesNotReference("Sandbox", "SilkEngine.Rendering");
        AssertProjectDoesNotReference("Sandbox", "SilkEngine.Rendering.Abstraction");
        AssertProjectDoesNotReference("Sandbox", "SilkEngine.Rendering.Backend");
        AssertProjectDoesNotReference("Sandbox", "SilkEngine.Rendering.OpenGL");
    }

    /// <summary>
    /// Sandbox 只直接引用 Host；本断言为 Spec 中"只引用 Host/Runtime 公开 API 的 Sandbox，
    /// 不因内部类型/程序集/测试专用 API 产生依赖"的可执行形式——Sandbox 程序集不得直接引用
    /// Rendering 域机制程序集（Rendering/Abstraction/Backend/OpenGL 均属引擎内部渲染机制，
    /// 业务层不得触达）；Runtime/Assets/Scene 属公开消费面（全局门面 Input/Math 等按架构存在于 Runtime）。
    /// </summary>
    [Fact]
    public void Sandbox_DoesNotReferenceRenderMachineryAssemblyNames()
    {
        var references = Assembly.Load("Sandbox").GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name?.StartsWith("SilkEngine.", StringComparison.Ordinal) == true)
            .ToList();

        Assert.DoesNotContain("SilkEngine.Rendering", references);
        Assert.DoesNotContain("SilkEngine.Rendering.Abstraction", references);
        Assert.DoesNotContain("SilkEngine.Rendering.Backend", references);
        Assert.DoesNotContain("SilkEngine.Rendering.OpenGL", references);
    }

    private static void AssertProjectReferences(string project, string referenced)
    {
        var content = ReadProjectFile(project);
        var reference = Normalize($"../{referenced}/{referenced}.csproj");
        Assert.True(
            ContainsReference(content, reference),
            $"{project} 应引用 {referenced}（未找到 ProjectReference '{reference}'）");
    }

    private static void AssertProjectDoesNotReference(string project, string referenced)
    {
        var path = Path.Combine(RepoRoot, "src", project, $"{project}.csproj");
        if (!File.Exists(path))
            return; // 项目未拆分为独立程序集（如 Threading 位于 Runtime 内）， vacuous 通过
        var content = File.ReadAllText(path);
        var reference = Normalize($"../{referenced}/{referenced}.csproj");
        Assert.False(
            ContainsReference(content, reference),
            $"{project} 不得引用 {referenced}（发现违规 ProjectReference）");
    }

    private static bool ContainsReference(string content, string normalizedReference)
        => Normalize(content).Contains(normalizedReference, StringComparison.Ordinal);

    private static string Normalize(string text)
        => text.Replace('\\', '/').Replace("//", "/", StringComparison.Ordinal);

    private static string ReadProjectFile(string projectName)
    {
        var path = Path.Combine(RepoRoot, "src", projectName, $"{projectName}.csproj");
        Assert.True(File.Exists(path), $"项目文件不存在：{path}");
        return File.ReadAllText(path);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SilkEngine.slnx")))
            dir = dir.Parent;
        return (dir ?? throw new InvalidOperationException("未找到仓库根目录（SilkEngine.slnx）")).FullName;
    }
}
