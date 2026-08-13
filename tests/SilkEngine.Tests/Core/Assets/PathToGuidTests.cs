using SilkEngine.Core.Assets;

namespace SilkEngine.Tests.Core.Assets;

public class PathToGuidTests
{
    [Fact]
    public void SamePath_ReturnsSameGuid()
    {
        Assert.Equal(AssetManager.PathToGuid("Assets/Red.png"), AssetManager.PathToGuid("Assets/Red.png"));
    }

    [Fact]
    public void DifferentPaths_ReturnDifferentGuids()
    {
        Assert.NotEqual(AssetManager.PathToGuid("Assets/Red.png"), AssetManager.PathToGuid("Assets/Blue.png"));
    }

    [Fact]
    public void CaseAndSeparatorVariants_ReturnSameGuid()
    {
        var a = AssetManager.PathToGuid(@"Assets\Red.PNG");
        var b = AssetManager.PathToGuid("assets/red.png");
        Assert.Equal(a, b);
    }
}
