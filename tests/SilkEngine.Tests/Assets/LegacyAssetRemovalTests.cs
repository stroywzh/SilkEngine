using System;
using System.IO;
using System.Linq;

namespace SilkEngine.Tests.Assets;

/// <summary>
/// 遗留资产模型删除的静态边界锁：Rendering 域源码不得出现任何 Assets 域类型名（含注释），
/// RendererBase 只使用 AssetHandle/AssetSlot 承载资产，不得回退旧 Shader/Mesh 实例与跟踪赋值桥。
/// </summary>
public class LegacyAssetRemovalTests
{
    private static readonly string SourceRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../src"));

    private static string FindSource(string fileName)
    {
        var file = Directory.GetFiles(SourceRoot, fileName, SearchOption.AllDirectories).SingleOrDefault();
        return file ?? throw new FileNotFoundException($"{fileName} 未在 src 下找到");
    }

    [Fact]
    public void RenderingSources_ContainNoAssetDomainReferences()
    {
        // Rendering 域 4 个项目（含具体后端）源码零 Assets 域类型名
        var renderingRoots = new[]
        {
            "SilkEngine.Rendering",
            "SilkEngine.Rendering.Abstraction",
            "SilkEngine.Rendering.Backend",
            "SilkEngine.Rendering.OpenGL",
        };
        var forbidden = new[] { "AssetId", "AssetHandle", "IAssetPayload", "TextureAsset", "ShaderAsset", "MeshAsset", "MaterialAsset", "AssetManager", "AssetPipeline" };

        foreach (var folder in renderingRoots)
        {
            var files = Directory.EnumerateFiles(Path.Combine(SourceRoot, folder), "*.cs", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var source = File.ReadAllText(file);
                foreach (var token in forbidden)
                    Assert.DoesNotContain(token, source);
            }
        }
    }

    [Fact]
    public void RendererBase_UsesPayloadHandlesInsteadOfLegacyInstances()
    {
        var source = File.ReadAllText(FindSource("RendererBase.cs"));

        Assert.DoesNotContain("SetTracked" + "Ambient", source);
        Assert.DoesNotContain("public Shader", source);
        Assert.DoesNotContain("public Mesh", source);
        Assert.Contains("AssetHandle", source);
    }
}
