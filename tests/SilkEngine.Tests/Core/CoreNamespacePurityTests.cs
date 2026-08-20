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
        var rootCore = new[] { "Object.cs", "Time.cs", "EngineLoop.cs" }
            .Select(f => Path.Combine(RootDir, f));
        return Directory.GetFiles(CoreDir, "*.cs", SearchOption.AllDirectories)
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
