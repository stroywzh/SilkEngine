using SilkEngine.Core.Assets;
using SilkEngine.Render;
using SilkEngine.Tests.Core.Assets;
using Xunit;

namespace SilkEngine.Tests.Core.Assets;

// 契约 C3：经 AssetsFixture 注册的 AssetManager 实例访问（Part 1 落盘的资产测试通道；
// Services.Get<AssetManager> 由夹具在类生命周期内注册，与本集合内其他夹具串行）
[Collection("Assets")]
public class AssetManagerTryResolveTests : IClassFixture<AssetsFixture>
{
    private readonly AssetManager _am;

    public AssetManagerTryResolveTests(AssetsFixture fixture) => _am = fixture.Manager;

    [Fact]
    public void TryResolve_CachedReady_ReturnsSameAsset()
    {
        var shader = new Shader { Name = "S" };
        var guid = Guid.NewGuid();
        var entry = _am.Cache.GetOrAdd(guid);
        entry.Data = shader;
        entry.State = AssetState.Ready;

        Assert.Same(shader, _am.TryResolve<Shader>(guid));
    }

    [Fact]
    public void TryResolve_UnknownGuid_ReturnsNull()
        => Assert.Null(_am.TryResolve<Shader>(Guid.NewGuid()));

    [Fact]
    public void TryResolve_TypeMismatch_ReturnsNull()
    {
        var shader = new Shader { Name = "S" };
        var guid = Guid.NewGuid();
        var entry = _am.Cache.GetOrAdd(guid);
        entry.Data = shader;
        entry.State = AssetState.Ready;

        Assert.Null(_am.TryResolve<Mesh>(guid));
    }
}
