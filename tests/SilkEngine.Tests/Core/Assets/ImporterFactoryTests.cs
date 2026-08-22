using SilkEngine.Assets;
using SilkEngine.Assets.Importer;

namespace SilkEngine.Tests.Core.Assets;

[Collection("Assets")]
public class ImporterFactoryTests
{
    private sealed class RecordingDecoder : IImageDecoder
    {
        public int DecodeCalls { get; private set; }

        public ImageData Decode(byte[] raw)
        {
            DecodeCalls++;
            return new ImageData(1, 1, [255, 0, 0, 255]);
        }

        public bool CanDecode(string extension) => true;
    }

    [Fact]
    public void Create_Png_ReturnsTextureImporter_ImportingRedTexture()
    {
        var importer = ImporterFactory.Create(".png");
        var tex = Assert.IsType<Texture2D>(importer.Import(PngFixtures.RedPng));
        Assert.Equal("Texture", tex.Name);
        Assert.Equal(1, tex.ImageData.Width);
        Assert.Equal(255, tex.ImageData.Pixels[0]);
    }

    [Fact]
    public void Create_Jpg_ReturnsTextureImporter()
    {
        Assert.IsType<TextureImporter>(ImporterFactory.Create(".jpg"));
    }

    [Fact]
    public void Create_ExtensionIsCaseInsensitive()
    {
        var tex = Assert.IsType<Texture2D>(ImporterFactory.Create(".PNG").Import(PngFixtures.RedPng));
        Assert.Equal(1, tex.ImageData.Width);
    }

    [Fact]
    public void Create_UnsupportedExtension_Throws()
    {
        Assert.Throws<NotSupportedException>(() => ImporterFactory.Create(".txt"));
        Assert.Throws<NotSupportedException>(() => ImporterFactory.Create(""));
    }

    [Fact]
    public void Create_UsesDecodersDefault()
    {
        var previous = Decoders.Default;
        try
        {
            var recording = new RecordingDecoder();
            Decoders.Default = recording;
            ImporterFactory.Create(".png").Import(PngFixtures.RedPng);
            Assert.Equal(1, recording.DecodeCalls);
        }
        finally
        {
            Decoders.Default = previous;
        }
    }

    [Fact]
    public void Decoders_Default_IsStbImageSharp()
    {
        Assert.IsType<StbImageSharpDecoder>(Decoders.Default);
    }

    private sealed class RecordingImporter : IAssetImporter
    {
        public IAsset Import(byte[] raw, ImportSettings? settings = null) =>
            new Texture2D { Name = "Custom", ImageData = new ImageData(1, 1, [0, 255, 0, 255]) };
    }

    [Fact]
    public void Register_NewExtension_ThenCreate_HitsNewImporter()
    {
        var importer = new RecordingImporter();
        ImporterFactory.Register(".custom", _ => importer);
        Assert.Same(importer, ImporterFactory.Create(".custom"));
    }

    [Fact]
    public void Register_DuplicateExtension_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => ImporterFactory.Register(".png", _ => new RecordingImporter()));
    }

    [Fact]
    public void Register_DuplicateExtension_IgnoringCase_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => ImporterFactory.Register(".PNG", _ => new RecordingImporter()));
    }

    [Fact]
    public void Register_Create_PassesSettingsToFactory()
    {
        ImportSettings? received = null;
        var importer = new RecordingImporter();
        ImporterFactory.Register(".cstm", s =>
        {
            received = s;
            return importer;
        });
        var settings = new ImportSettings { Path = "assets/x.png" };
        ImporterFactory.Create(".CSTM", settings);
        Assert.Same(settings, received);
    }
}
