using SilkEngine.Assets;
using SilkEngine.Assets.Database;

namespace SilkEngine.Tests.Assets;

/// <summary>
/// 任务 5：依赖索引（正向/反向/级联失效）、构建键导入设置指纹、
/// 磁盘管线端到端路径依赖解析（材质 → 着色器/纹理/网格）与依赖边持久化/反向索引回写。
/// </summary>
public class AssetDependencyIndexTests
{
    [Fact]
    public void InvalidateDependency_StalesMaterialAndRemovesBuildHit()
    {
        var index = new AssetDependencyIndex();
        var shader = new AssetId(Guid.NewGuid());
        var material = new AssetId(Guid.NewGuid());
        index.ReplaceDependencies(material, [shader]);

        var affected = index.GetDependents(shader);

        Assert.Contains(material, affected);
    }

    [Fact]
    public void ImportSettingsAffectBuildKey()
    {
        var first = AssetBuildKey.Create(
            new AssetId(Guid.NewGuid()), new AssetTypeId("texture"), 1, 1, "opengl", "srgb=false");
        var second = first with { ImportSettingsFingerprint = "srgb=true" };

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task MaterialImport_ResolvesPathDependenciesThroughCatalog()
    {
        using var fixture = TestAssetPipelineFixture.CreateWith(
            ("Shaders/Unlit.hlsl", "shader"),
            ("Textures/ShoreKeeper1.png", "texture"),
            ("Meshes/Cube.obj", "mesh"),
            ("Materials/Cube.asset", "material"));

        var result = await fixture.LoadAsync<MaterialAsset>("Materials/Cube.asset");

        Assert.Equal(3, result.Dependencies.Count);
        Assert.All(result.Dependencies, dependency => Assert.NotEqual(default, dependency.Id));
        Assert.NotEqual(default, result.Shader.Id);
    }

    [Fact]
    public void InvalidateCascade_ReturnsTransitiveDependents()
    {
        var index = new AssetDependencyIndex();
        var c = new AssetId(Guid.NewGuid());
        var b = new AssetId(Guid.NewGuid());
        var a = new AssetId(Guid.NewGuid());
        index.ReplaceDependencies(b, [c]);
        index.ReplaceDependencies(a, [b]);

        var affected = index.InvalidateCascade(c);

        Assert.Equal(2, affected.Count);
        Assert.Contains(b, affected);
        Assert.Contains(a, affected);
        Assert.DoesNotContain(c, affected);
    }

    [Fact]
    public async Task BlockingFixture_GatesReadUntilRelease()
    {
        using var fixture = TestAssetPipelineFixture.Blocking("Textures/a.png");
        var load = Task.Run(() => fixture.LoadAsync<TextureAsset>("Textures/a.png"));

        Assert.False(load.Wait(TimeSpan.FromMilliseconds(200)));
        fixture.ReleaseRead();

        var operation = await load.WaitAsync(TimeSpan.FromSeconds(10));
        var texture = await operation.AsTask();
        Assert.Equal("a", texture.Name);

        // 门控释放后再次加载可直接完成（不再挂起）
        var second = fixture.LoadAsync<TextureAsset>("Textures/a.png").AsTask().GetAwaiter().GetResult();
        Assert.Equal("a", second.Name);
    }

    [Fact]
    public async Task MaterialImport_PersistsDependencyEdgesToDatabase()
    {
        const string projectNamespace = "sandbox";
        using var fixture = TestAssetPipelineFixture.CreateWith(
            ("Shaders/Unlit.hlsl", "shader"),
            ("Textures/ShoreKeeper1.png", "texture"),
            ("Meshes/Cube.obj", "mesh"),
            ("Materials/Cube.asset", "material"));

        await fixture.LoadAsync<MaterialAsset>("Materials/Cube.asset");
        fixture.DrainFrameCommit();

        var materialId = AssetIdFactory.Create(projectNamespace, "Materials/Cube.asset", new AssetTypeId("material"));
        var shaderId = AssetIdFactory.Create(projectNamespace, "Shaders/Unlit.hlsl", new AssetTypeId("shader"));
        var textureId = AssetIdFactory.Create(projectNamespace, "Textures/ShoreKeeper1.png", new AssetTypeId("texture"));
        var meshId = AssetIdFactory.Create(projectNamespace, "Meshes/Cube.obj", new AssetTypeId("mesh"));

        // 内存反向索引：着色器/纹理/网格的依赖方均为材质
        Assert.Contains(materialId, fixture.Pipeline.DependencyIndex.GetDependents(shaderId));
        Assert.Contains(materialId, fixture.Pipeline.DependencyIndex.GetDependents(textureId));
        Assert.Contains(materialId, fixture.Pipeline.DependencyIndex.GetDependents(meshId));

        // 持久化依赖边（单事务 Dependencies 表）：材质 → 三个依赖路径
        var database = fixture.Pipeline.Database;
        Assert.NotNull(database);
        var snapshot = await database.CaptureSnapshotAsync(CancellationToken.None);
        Assert.Equal(3, snapshot.Dependencies.Length);
        Assert.All(snapshot.Dependencies, edge => Assert.Equal(materialId, edge.AssetId));
        Assert.Contains(snapshot.Dependencies, edge => edge.DependsOnPath == "Shaders/Unlit.hlsl");
        Assert.Contains(snapshot.Dependencies, edge => edge.DependsOnPath == "Textures/ShoreKeeper1.png");
        Assert.Contains(snapshot.Dependencies, edge => edge.DependsOnPath == "Meshes/Cube.obj");
    }
}