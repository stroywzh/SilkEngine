using System.IO;
using System.Linq;

namespace SilkEngine.Tests.Render;

public class RenderNamespacePurityTests
{
    // SilkEngine.Render 命名空间文件分布在 Assets 项目（Material*/MeshFactory/DefaultTextures）
    // 与 Rendering.OpenGL 项目（DefaultWindowOption）；命名空间与物理目录解耦是既有约定
    private static readonly string AssetsDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../src/SilkEngine.Assets")
    );
    private static readonly string OpenGLDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../src/SilkEngine.Rendering.OpenGL")
    );

    private static IEnumerable<string> RenderFiles() =>
        Directory.GetFiles(AssetsDir, "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(OpenGLDir, "*.cs", SearchOption.AllDirectories))
            .Where(f => File.ReadAllText(f).Contains("namespace SilkEngine.Render;", StringComparison.Ordinal));

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
