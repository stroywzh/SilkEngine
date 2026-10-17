using SilkEngine.Rendering;
using Xunit;

namespace SilkEngine.Rendering.Tests;

public sealed class RenderContextTests
{
    [Fact]
    public void Constructor_PreservesUnsignedIdAndPreviousFrameDelta()
    {
        var frame = new RenderContext(ulong.MaxValue, 0.125);
        Assert.Equal(ulong.MaxValue, frame.FrameId);
        Assert.Equal(0.125, frame.DeltaSeconds);
    }

    [Fact]
    public void Constructor_RejectsZeroIdAndInvalidDelta()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RenderContext(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RenderContext(1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RenderContext(1, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RenderContext(1, double.PositiveInfinity));
    }
}
