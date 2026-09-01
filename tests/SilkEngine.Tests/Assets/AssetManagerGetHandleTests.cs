using System;
using System.IO;
using SilkEngine.Assets;
using SilkEngine.Host;

namespace SilkEngine.Tests.Assets;

/// <summary>
/// GetHandle 契约：已索引路径返回稳定句柄（与 Load 解析到同一 Payload），
/// 未索引路径抛详细 InvalidOperationException；Sandbox 据此绑定渲染器资产属性而无需自造 Handle。
/// </summary>
[Collection("Assets")]
public class AssetManagerGetHandleTests : IDisposable
{
    private readonly EngineHost _host;
    private readonly string _root;

    public AssetManagerGetHandleTests()
    {
        // 1x1 PNG（最小合法纹理，TextureImporter 可导入）
        const string pngBase64 =
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";
        _root = Path.Combine(Path.GetTempPath(), "kilo-gethandle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(Path.Combine(_root, "tex.png"), Convert.FromBase64String(pngBase64));

        _host = EngineHost.Create(b =>
        {
            b.UseHeadlessForTests();
            b.UseAssetRoot(_root);
        });
        _host.Initialize();
    }

    public void Dispose()
    {
        _host.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void GetHandle_ReturnsStableHandleResolvingSamePayloadAsLoad()
    {
        var payload = _host.AssetManager.Load<TextureAsset>("tex.png");
        // Load 同步返回 Payload；缓存条目在 FrameCommit 阶段应用，先驱动一帧
        _host.Loop.StepFrame();

        var handle = _host.AssetManager.GetHandle<TextureAsset>("tex.png");

        Assert.NotEqual(default, handle);
        Assert.True(_host.AssetManager.TryResolve(handle, out var resolved));
        Assert.Same(payload, resolved);
    }

    [Fact]
    public void GetHandle_RepeatedCallsReturnSameAssetId()
    {
        var first = _host.AssetManager.GetHandle<TextureAsset>("tex.png");
        var second = _host.AssetManager.GetHandle<TextureAsset>("tex.png");

        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public void GetHandle_UnknownPathThrowsDetailedException()
    {
        Assert.Throws<InvalidOperationException>(
            () => _host.AssetManager.GetHandle<TextureAsset>("missing.png"));
    }
}
