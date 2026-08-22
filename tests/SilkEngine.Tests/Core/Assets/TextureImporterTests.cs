using SilkEngine.Core;
using SilkEngine.Assets;
using SilkEngine.Assets.Importer;

namespace SilkEngine.Tests.Core.Assets;

[Collection("Assets")]
public class TextureImporterTests : IDisposable
{
    /// <summary>测试级清理：注销测试内 ctor 自注册的 AssetManager 实例（Unregister 幂等）</summary>
    public void Dispose() => Services.Unregister<AssetManager>();

    private sealed class RecordingDecoder : IImageDecoder
    {
        public ImageData Decode(byte[] raw) => new(1, 1, [255, 0, 0, 255]);
        public bool CanDecode(string extension) => true;
    }

    [Fact]
    public void Import_WithPathSetting_SetsNameFromFileNameWithoutExtension()
    {
        var importer = new TextureImporter(new RecordingDecoder());
        var tex = Assert.IsType<Texture2D>(
            importer.Import(PngFixtures.RedPng, new ImportSettings { Path = @"C:\Assets\hero.png" }));
        Assert.Equal("hero", tex.Name);
    }

    [Fact]
    public void Import_SubDirectoryPath_DerivesNameOnly()
    {
        var importer = new TextureImporter(new RecordingDecoder());
        var tex = Assert.IsType<Texture2D>(
            importer.Import(PngFixtures.RedPng, new ImportSettings { Path = "assets/UI/icon.png" }));
        Assert.Equal("icon", tex.Name);
    }

    [Fact]
    public void Import_NoSettings_FallsBackToTexture()
    {
        var importer = new TextureImporter(new RecordingDecoder());
        var tex = Assert.IsType<Texture2D>(importer.Import(PngFixtures.RedPng));
        Assert.Equal("Texture", tex.Name);
    }

    [Fact]
    public void Load_ViaManager_SetsNameFromPath()
    {
        using var file = PngTestFile.Create();
        var am = new AssetManager(new RecordingScheduler());
        var tex = am.Load<Texture2D>(file.FilePath);
        Assert.Equal(Path.GetFileNameWithoutExtension(file.FilePath), tex.Name);
    }
}
