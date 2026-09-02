using System;
using System.IO;
using SilkEngine.Assets;
using SilkEngine.Host;

namespace SilkEngine.Tests.Assets;

// 类名与命名空间末段同名（Unity 式门面）：裸标识符 Assets 会按外层命名空间成员解析为命名空间；
// 编译单元级别名在查找顺序上晚于外层命名空间成员，故别名须置于文件范围命名空间声明之后
using Assets = SilkEngine.Assets.Assets;

/// <summary>
/// Unity 式静态 Assets 门面生命周期：未初始化时抛错、Initialize 绑定宿主 AssetManager、Dispose 解绑。
/// Bind 是进程级单槽静态状态，与全部创建 EngineHost 的测试类同集合串行。
/// </summary>
[Collection("Assets")]
public class AssetsFacadeTests : IDisposable
{
    private readonly string _root;

    public AssetsFacadeTests() => _root = TestTempDirectory.Create();

    public void Dispose() => TestTempDirectory.Delete(_root);

    [Fact]
    public void LoadBeforeHostInitialization_Throws()
    {
        Assets.ResetForTests();

        var exception = Assert.Throws<InvalidOperationException>(
            () => Assets.Load<TextureAsset>("Textures/ShoreKeeper1.png"));

        Assert.Contains("initialized", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InitializedHost_BindsStaticFacadeToItsAssetManager()
    {
        using var host = CreateHostWithTempAssetRoot();

        var handle = Assets.GetHandle<TextureAsset>("Textures/ShoreKeeper1.png");

        Assert.NotEqual(default, handle.Id);
    }

    [Fact]
    public void Dispose_UnbindsStaticFacade()
    {
        var host = CreateHostWithTempAssetRoot();
        host.Dispose();

        Assert.Throws<InvalidOperationException>(
            () => Assets.GetHandle<TextureAsset>("Textures/ShoreKeeper1.png"));
    }

    /// <summary>创建已初始化的无头宿主：临时资产根含 Textures/ShoreKeeper1.png（1x1 PNG；GetHandle 只要求路径进入 VFS 索引，不触发解码）。</summary>
    private EngineHost CreateHostWithTempAssetRoot()
    {
        const string pngBase64 =
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";
        Directory.CreateDirectory(Path.Combine(_root, "Textures"));
        File.WriteAllBytes(
            Path.Combine(_root, "Textures", "ShoreKeeper1.png"),
            Convert.FromBase64String(pngBase64));

        var host = EngineHost.Create(b =>
        {
            b.UseHeadlessForTests();
            b.UseAssetRoot(_root);
        });
        host.Initialize();
        return host;
    }
}
