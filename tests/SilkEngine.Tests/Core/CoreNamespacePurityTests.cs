using System.IO;
using System.Linq;

namespace SilkEngine.Tests.Core;

public class CoreNamespacePurityTests
{
    private static readonly string RootDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../src/SilkEngine")
    );
    private static readonly string CoreDir = Path.Combine(RootDir, "Core");

    private static IEnumerable<string> CoreFiles()
    {
        // EngineLoop 为顶层编排者，允许依赖 Scene（架构决策）；纯净范围 = Core/ 子目录 + 根目录基础类型 Object/Time
        // FrameCommitter 为帧末提交编排器（A.4 拆分自 EngineLoop.CommitFrame），依赖 Scene 提交机制，同为编排层豁免
        var rootCore = new[] { "Object.cs", "Time.cs" }
            .Select(f => Path.Combine(RootDir, f));
        var compositionRoots = new[] { "FrameCommitter.cs" };
        return Directory.GetFiles(CoreDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !compositionRoots.Contains(Path.GetFileName(f)))
            .Concat(rootCore);
    }

    [Fact]
    public void CoreFiles_DoNotReference_SceneNamespace()
    {
        var files = CoreFiles().ToArray();
        Assert.NotEmpty(files);
        foreach (var f in files)
        {
            var text = File.ReadAllText(f);
            Assert.DoesNotContain("using SilkEngine.Scene;", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CoreFiles_DoNotReference_RenderAndThreadingNamespaces()
    {
        foreach (var f in CoreFiles())
        {
            var text = File.ReadAllText(f);
            Assert.DoesNotContain("using SilkEngine.Render;", text, StringComparison.Ordinal);
            Assert.DoesNotContain("using SilkEngine.Threading;", text, StringComparison.Ordinal);
        }
    }
}
