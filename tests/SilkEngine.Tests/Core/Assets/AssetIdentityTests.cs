using SilkEngine.Assets;
using SilkEngine.Assets.VirtualFileSystem;

namespace SilkEngine.Tests.Core.Assets;

/// <summary>资产身份模型测试：AssetId 与 VirtualNodeId 类型区分、逻辑路径错误语义</summary>
public class AssetIdentityTests
{
    [Fact]
    public void AssetIds_AreDifferentTypes()
    {
        var asset = new AssetId(Guid.NewGuid());
        var node = new VirtualNodeId(asset.Value);
        Assert.NotEqual(asset.GetType(), node.GetType());
    }

    [Fact]
    public void InvalidLogicalPath_UsesArgumentException()
    {
        var fs = new InMemoryAssetFileSystem("Assets");
        Assert.Throws<ArgumentException>(() => { fs.Normalize("../outside.png"); });
    }
}
