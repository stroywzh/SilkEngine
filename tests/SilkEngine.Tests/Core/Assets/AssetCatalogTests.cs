using SilkEngine.Assets;
using SilkEngine.Assets.Importer;

namespace SilkEngine.Tests.Core.Assets;

/// <summary>资产目录与导入器注册表测试：目录身份稳定性、按 ID 查询、注册表解析与扩展名规范化</summary>
public class AssetCatalogTests
{
    [Fact]
    public void Catalog_AssignsStableAssetIdPerSourceAndType()
    {
        var catalog = new AssetCatalog();
        var node = new VirtualNodeId(Guid.NewGuid());

        var a = catalog.GetOrAdd(node, new AssetTypeId("texture"));
        var b = catalog.GetOrAdd(node, new AssetTypeId("texture"));

        Assert.Equal(a.AssetId, b.AssetId);
    }

    [Fact]
    public void Catalog_DifferentTypes_AssignDistinctAssetIds()
    {
        var catalog = new AssetCatalog();
        var node = new VirtualNodeId(Guid.NewGuid());

        var texture = catalog.GetOrAdd(node, new AssetTypeId("texture"));
        var audio = catalog.GetOrAdd(node, new AssetTypeId("audio"));

        Assert.NotEqual(texture.AssetId, audio.AssetId);
    }

    [Fact]
    public void Catalog_DifferentSources_AssignDistinctAssetIds()
    {
        var catalog = new AssetCatalog();

        var a = catalog.GetOrAdd(new VirtualNodeId(Guid.NewGuid()), new AssetTypeId("texture"));
        var b = catalog.GetOrAdd(new VirtualNodeId(Guid.NewGuid()), new AssetTypeId("texture"));

        Assert.NotEqual(a.AssetId, b.AssetId);
    }

    [Fact]
    public void Catalog_TryGetById_ReturnsSameRecord()
    {
        var catalog = new AssetCatalog();
        var record = catalog.GetOrAdd(new VirtualNodeId(Guid.NewGuid()), new AssetTypeId("texture"));

        Assert.True(catalog.TryGet(record.AssetId, out var byId));
        Assert.Same(record, byId);
    }

    [Fact]
    public void MissingImporter_ThrowsNotSupportedException()
    {
        var registry = new AssetImporterRegistry();
        Assert.Throws<NotSupportedException>(() => registry.Resolve(
            new AssetTypeId("unknown"), ".unknown"));
    }

    [Fact]
    public void Registry_ResolvesPngByTypeAndExtension()
    {
        var registry = new AssetImporterRegistry();

        var importer = registry.Resolve(new AssetTypeId("texture"), ".png");

        Assert.IsType<TextureImporter>(importer);
    }

    [Fact]
    public void Registry_ExtensionIsCaseInsensitiveAndDotTolerant()
    {
        var registry = new AssetImporterRegistry();

        Assert.IsType<TextureImporter>(registry.Resolve(new AssetTypeId("texture"), "PNG"));
        Assert.IsType<TextureImporter>(registry.Resolve(new AssetTypeId("texture"), ".Jpg"));
    }

    [Fact]
    public void Registry_DuplicateRegistration_ThrowsInvalidOperationException()
    {
        var registry = new AssetImporterRegistry();

        Assert.Throws<InvalidOperationException>(() => registry.Register(
            new AssetTypeId("texture"), ".png", _ => new TextureImporter(new StbImageSharpDecoder())));
    }

    [Fact]
    public void Registry_ResolveWrongType_ThrowsNotSupportedException()
    {
        var registry = new AssetImporterRegistry();

        Assert.Throws<NotSupportedException>(() => registry.Resolve(new AssetTypeId("audio"), ".png"));
    }

    [Fact]
    public void ImporterFactory_StillResolvesDefaultExtensions()
    {
        Assert.IsType<TextureImporter>(ImporterFactory.Create(".png"));
    }
}
