using System;
using System.IO;
using System.Linq;

namespace SilkEngine.Tests.Assets;

/// <summary>
/// 遗留资产模型删除的静态边界锁：Rendering 域源码不得出现任何 Assets 域类型名（含注释），
/// RendererBase 只使用 AssetHandle/AssetSlot 承载资产，不得回退旧 Shader/Mesh 实例与 SetTrackedAmbient。
/// </summary>
public class LegacyAssetRemovalTests
{
    private static readonly string SourceRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../src/SilkEngine"));

    private static string FindSource(string fileName)
    {
        var file = Directory.GetFiles(SourceRoot, fileName, SearchOption.AllDirectories).SingleOrDefault();
        return file ?? throw new FileNotFoundException($"{fileName} 未在 src/SilkEngine 下找到");
    }

    private static string FindSourceRoot(string folderName)
    {
        var dir = Path.Combine(SourceRoot, folderName);
        if (!Directory.Exists(dir))
            throw new DirectoryNotFoundException($"{dir} 目录不存在");
        return dir;
    }

    [Fact]
    public void RenderingSources_ContainNoAssetDomainReferences()
    {
        var files = Directory.EnumerateFiles(FindSourceRoot("Rendering"), "*.cs", SearchOption.AllDirectories);
        var forbidden = new[] { "AssetId", "AssetHandle", "IAssetPayload", "TextureAsset", "ShaderAsset", "MeshAsset", "MaterialAsset", "AssetManager", "AssetPipeline" };

        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            foreach (var token in forbidden)
                Assert.DoesNotContain(token, source);
        }
    }

    [Fact]
    public void RendererBase_UsesPayloadHandlesInsteadOfLegacyInstances()
    {
        var source = File.ReadAllText(FindSource("RendererBase.cs"));

        Assert.DoesNotContain("SetTrackedAmbient", source);
        Assert.DoesNotContain("public Shader", source);
        Assert.DoesNotContain("public Mesh", source);
        Assert.Contains("AssetHandle", source);
    }
}
