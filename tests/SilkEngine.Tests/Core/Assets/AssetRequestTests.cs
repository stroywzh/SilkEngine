using SilkEngine.Core.Assets;

namespace SilkEngine.Tests.Core.Assets;

[Collection("Assets")]
public class AssetRequestTests
{
    private sealed class FakeAsset : IAsset { }

    [Fact]
    public void GetAwaiter_ReturnsSelf()
    {
        var req = new AssetRequest<Texture2D>();
        Assert.Same(req, req.GetAwaiter());
    }

    [Fact]
    public void IsCompleted_FollowsIsDone()
    {
        var req = new AssetRequest<Texture2D>();
        Assert.False(req.IsCompleted);
        req.Complete(new Texture2D(), null);
        Assert.True(req.IsCompleted);
    }

    [Fact]
    public void Complete_SetsAssetAndProgress_AndInvokesContinuation()
    {
        var req = new AssetRequest<Texture2D>();
        var continuationRan = false;
        req.OnCompleted(() => continuationRan = true);
        var tex = new Texture2D();
        req.Complete(tex, null);
        Assert.True(continuationRan);
        Assert.True(req.IsDone);
        Assert.Same(tex, req.Asset);
        Assert.Equal(1f, req.Progress);
        Assert.Null(req.Error);
    }

    [Fact]
    public void GetResult_ReturnsAsset_WhenSuccess()
    {
        var req = new AssetRequest<Texture2D>();
        var tex = new Texture2D();
        req.Complete(tex, null);
        Assert.Same(tex, req.GetResult());
    }

    [Fact]
    public void GetResult_ThrowsError_WhenFailed()
    {
        var req = new AssetRequest<Texture2D>();
        var error = new InvalidOperationException("boom");
        req.Complete(null, error);
        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => req.GetResult()));
    }

    [Fact]
    public void IAssetRequest_Complete_TypeMismatch_SetsError()
    {
        var req = new AssetRequest<Texture2D>();
        ((IAssetRequest)req).Complete(new FakeAsset(), null);
        Assert.True(req.IsDone);
        Assert.Null(req.Asset);
        Assert.IsType<InvalidOperationException>(req.Error);
    }

    [Fact]
    public void Completed_Factory_ReturnsDoneRequest()
    {
        var tex = new Texture2D();
        var req = AssetRequest<Texture2D>.Completed(tex);
        Assert.True(req.IsDone);
        Assert.Same(tex, req.Asset);
        Assert.Null(req.Error);
        Assert.Equal(1f, req.Progress);
    }
}
