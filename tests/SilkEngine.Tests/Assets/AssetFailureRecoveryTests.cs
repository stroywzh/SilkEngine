using SilkEngine.Assets;
using SilkEngine.Core;

namespace SilkEngine.Tests.Assets;

/// <summary>
/// 失败恢复与帧末驱逐（任务 9）：失败结果不缓存负产物，修复源文件并 Invalidate 后
/// 以新 BuildKey 重试成功；UnloadUnused 出缓存并把 GPU release 请求入队。
/// </summary>
[Collection("Assets")]
public class AssetFailureRecoveryTests : IDisposable
{
    /// <summary>测试级清理：注销 ctor 自注册的 AssetManager 实例（Unregister 幂等）</summary>
    public void Dispose() => Services.Unregister<AssetManager>();

    [Fact]
    public async Task FailedImport_SourceFixAndInvalidate_AllowsRetry()
    {
        using var fixture = TestAssetPipelineFixture.WithMutableFile("Meshes/Cube.obj", "v 1 2");
        await Assert.ThrowsAsync<InvalidDataException>(
            () => fixture.Manager.LoadAsync<MeshAsset>("Meshes/Cube.obj").AsTask());

        fixture.Replace("Meshes/Cube.obj", TestAssetData.ValidCubeObj);
        fixture.Manager.Invalidate("Meshes/Cube.obj");

        Assert.NotNull(await fixture.Manager.LoadAsync<MeshAsset>("Meshes/Cube.obj").AsTask());
    }

    [Fact]
    public void UnloadUnused_QueuesGpuReleaseAndDoesNotRetainPayload()
    {
        using var fixture = AssetManagerTestFixture.ReadyTexture();
        fixture.Manager.UnloadUnused();

        Assert.False(fixture.Manager.TryResolve(fixture.Handle, out _));
        Assert.NotEmpty(fixture.Manager.DrainReleaseRequestsForTests());
    }
}