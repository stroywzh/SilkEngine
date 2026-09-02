using System;
using System.IO;
using System.Linq;
using SilkEngine.Assets;
using SilkEngine.Host;

namespace SilkEngine.Tests.Host;

// 类名与命名空间末段同名（Unity 式门面）：裸标识符 Assets 会按外层命名空间成员解析为命名空间；
// 编译单元级别名在查找顺序上晚于外层命名空间成员，故别名须置于文件范围命名空间声明之后
using Assets = SilkEngine.Assets.Assets;

/// <summary>
/// Sandbox 磁盘资产黑盒闭环：正式 AssetRoot（Assets/）资源经静态 <see cref="Assets"/> 门面
/// 在 headless 宿主上完整走 Load/GetHandle 解析（Cube.asset → material，shader/obj/PNG 依赖闭环）。
/// 运行于 Tests 输出目录（cwd），AssetRoot 副本经 tests csproj 从 src/Sandbox/Assets 复制。
/// </summary>
[Collection("Assets")]
public class SandboxDiskAssetSmokeTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void SandboxProject_ReferencesOnlyHostAndUsesStaticAssetFacade()
    {
        var project = File.ReadAllText(FindSandboxProject());
        var source = ReadSandboxSources();

        Assert.Contains("SilkEngine.Host", project, StringComparison.Ordinal);
        Assert.DoesNotContain("AssetPipeline", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RenderThreadHost", source, StringComparison.Ordinal);
        Assert.Contains("Assets.Load", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DemoAssets.NewId", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DiskAssetSmoke_LoadsCubeMaterialAndAllDependencies()
    {
        using var host = EngineHost.Create(builder =>
        {
            builder.UseHeadlessForTests();
            builder.UseAssetRoot("Assets");
        });
        host.Initialize();

        var material = Assets.Load<MaterialAsset>("Materials/Cube.asset");
        var mesh = Assets.GetHandle<MeshAsset>("Meshes/Cube.obj");
        var texture = Assets.GetHandle<TextureAsset>("Textures/ShoreKeeper1.png");
        var shader = Assets.Load<ShaderAsset>("Shaders/Unlit.hlsl");

        Assert.NotNull(material);
        Assert.NotEqual(default, mesh.Id);
        Assert.NotEqual(default, texture.Id);
        Assert.Equal("vert", shader.VertexEntryPoint);
        Assert.Equal("frag", shader.FragmentEntryPoint);
    }

    private static string FindSandboxProject() => FindSource("src/Sandbox/Sandbox.csproj");

    private static string ReadSandboxSources()
    {
        var root = Path.Combine(RepoRoot, "src", "Sandbox");
        return string.Join(
            "\n",
            Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));
    }

    private static string FindSource(string relativePath)
    {
        var file = Path.Combine(RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(file)
            ? file
            : throw new FileNotFoundException($"{relativePath} 未找到");
    }
}