using System.IO;
using System.Linq;

namespace SilkEngine.Tests.Render;

public class RenderNamespacePurityTests
{
    private static readonly string RootDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../src/SilkEngine")
    );
    private static readonly string RenderDir = Path.Combine(RootDir, "Render");

    private static IEnumerable<string> RenderFiles() =>
        Directory.GetFiles(RenderDir, "*.cs", SearchOption.AllDirectories);

    [Fact]
    public void RenderFiles_DoNotReference_SceneNamespace()
    {
        var files = RenderFiles().ToArray();
        Assert.NotEmpty(files);
        foreach (var f in files)
        {
            var text = File.ReadAllText(f);
            Assert.DoesNotContain("using SilkEngine.Scene;", text, StringComparison.Ordinal);
        }
    }
}
