using SilkEngine.Assets;
using SilkEngine.Assets.Importer;
using SilkEngine.Assets.Serialization;
using SilkEngine.Assets.VirtualFileSystem;
using SilkEngine.Core;
using SilkEngine.Render;
using SilkEngine.Tests.Core;

namespace SilkEngine.Tests.Core.Assets;

/// <summary>
/// 资产序列化集成测试：AssetManager 注入序列化器注册表（实例隔离、无全局状态）、
/// 受控 resolver 视图按缓存解析已加载资产、材质资产记录不含实例覆盖/管理器字样。
/// </summary>
[Collection("Assets")]
public class AssetSerializationIntegrationTests : IDisposable
{
    /// <summary>测试级清理：注销 ctor 自注册的 AssetManager 实例（Unregister 幂等）</summary>
    public void Dispose() => Services.Unregister<AssetManager>();

    [Fact]
    public void AssetManager_UsesInjectedSerializerRegistryWithoutGlobalState()
    {
        var first = Fixtures.AssetManagerWithSerializerRegistry();
        var second = Fixtures.AssetManagerWithSerializerRegistry();
        var type = new AssetTypeId("custom");

        first.RegisterSerializer(new TestSerializer(type, 1, 1));

        Assert.NotNull(first.ResolveSerializer(type, 1));
        Assert.Throws<NotSupportedException>(() => second.ResolveSerializer(type, 1));
    }

    [Fact]
    public void MaterialInstanceOverridesAreNotSerializedAsMaterialAssetData()
    {
        var material = Fixtures.MaterialInstanceWithOverride();
        var record = Fixtures.SerializeMaterialAsset(material.Source);

        Assert.DoesNotContain("Overrides", record.Data);
        Assert.DoesNotContain("AssetManager", record.Data);
    }

    [Fact]
    public void AssetManager_ResolverViewResolvesLoadedAssetsFromCache()
    {
        var files = new InMemoryAssetFileSystem("Assets");
        files.Add("a.png", PngFixtures.RedPng);
        var assets = Fixtures.AssetManagerWithSerializerRegistry(files);

        var tex = assets.Load<TextureAsset>("a.png");
        var id = Assert.Single(assets.Cache.All()).AssetId;

        Assert.Same(tex, assets.Resolver.Resolve(new AssetHandle<TextureAsset>(id)));
        Assert.Null(assets.Resolver.Resolve(new AssetHandle<TextureAsset>(new AssetId(Guid.NewGuid()))));
        Assert.Null(assets.Resolver.Resolve(new UntypedAssetHandle(new AssetId(Guid.NewGuid()))));
        Assert.Null(assets.Resolver.TryGetRecord(new AssetId(Guid.NewGuid())));
    }
}
