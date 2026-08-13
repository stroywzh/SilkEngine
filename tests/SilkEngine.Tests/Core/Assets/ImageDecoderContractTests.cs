using SilkEngine.Core.Assets;
using SilkEngine.Core.Assets.Importer;

namespace SilkEngine.Tests.Core.Assets;

[Collection("Assets")]
public class ImageDecoderContractTests
{
    public static TheoryData<IImageDecoder> Decoders => new()
    {
        new StbImageSharpDecoder(),
        new StbiSharpDecoder(),
    };

    [Theory]
    [MemberData(nameof(Decoders))]
    public void Decode_RedPng_Returns1x1RedPixel(IImageDecoder decoder)
    {
        var data = decoder.Decode(PngFixtures.RedPng);
        Assert.Equal(1, data.Width);
        Assert.Equal(1, data.Height);
        Assert.Equal(4, data.Pixels.Length);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, data.Pixels);
    }

    [Theory]
    [MemberData(nameof(Decoders))]
    public void Decode_CorruptPng_ThrowsInvalidOperationException(IImageDecoder decoder)
    {
        Assert.ThrowsAny<InvalidOperationException>(() => decoder.Decode(PngFixtures.CorruptPng));
    }

    [Theory]
    [MemberData(nameof(Decoders))]
    public void CanDecode_RecognizesSupportedExtensions(IImageDecoder decoder)
    {
        Assert.True(decoder.CanDecode(".png"));
        Assert.True(decoder.CanDecode(".jpg"));
        Assert.True(decoder.CanDecode(".PNG"));
        Assert.False(decoder.CanDecode(".txt"));
        Assert.False(decoder.CanDecode("png"));
        Assert.False(decoder.CanDecode(""));
    }

    [Fact]
    public void BothDecoders_ProduceIdenticalPixels()
    {
        var a = new StbImageSharpDecoder().Decode(PngFixtures.RedPng);
        var b = new StbiSharpDecoder().Decode(PngFixtures.RedPng);
        Assert.Equal(a.Width, b.Width);
        Assert.Equal(a.Height, b.Height);
        Assert.Equal(a.Pixels, b.Pixels);
    }
}
