using System.IO;
using System.Linq;

namespace SilkEngine.Tests.Rendering.OpenGL;

/// <summary>
/// Rendering.OpenGL 命名空间边界测试：OpenGL backend 只消费 Rendering 契约，
/// 源文件不得出现任何 Assets 域类型名（含注释）。
/// </summary>
public class NamespaceBoundaryTests
{
    private static readonly string[] BannedTokens =
    [
        "AssetManager", "AssetPipeline", "AssetHandle", "IAssetPayload",
        "TextureAsset", "ShaderAsset", "MeshAsset", "MaterialAsset",
    ];

    private static string FindSource(string fileName)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/SilkEngine"));
        var file = Directory.GetFiles(root, fileName, SearchOption.AllDirectories).SingleOrDefault();
        return file ?? throw new FileNotFoundException($"{fileName} 未在 src/SilkEngine 下找到");
    }

    [Fact]
    public void OpenGlBackend_ConsumesOnlyRenderingContracts()
    {
        var source = File.ReadAllText(FindSource("OpenGLRenderBackend.cs"));

        foreach (var token in BannedTokens)
            Assert.DoesNotContain(token, source);
        Assert.Contains("IRenderBackend", source);
    }

    [Fact]
    public void OpenGlSources_DoNotReferenceAssetDomainTypes()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/SilkEngine"));
        var openGlDir = Path.Combine(root, "Rendering", "OpenGL");
        Assert.True(Directory.Exists(openGlDir), "Rendering/OpenGL 目录不存在");

        foreach (var f in Directory.GetFiles(openGlDir, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(f);
            foreach (var token in BannedTokens)
                Assert.DoesNotContain(token, text);
        }
    }
}
